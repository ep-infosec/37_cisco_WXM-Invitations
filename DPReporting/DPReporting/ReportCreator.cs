using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentDateTime;
using XM.ID.Net;
using XM.ID.Invitations.Net;
using Newtonsoft.Json;

namespace DPReporting
{
    public class ReportCreator
    {
        IConfigurationRoot Configuration;
        readonly string WXM_BASE_URL;
        ApplicationLog log;
        private readonly ViaMongoDB via;
        private readonly List<Question> questions;
        private readonly List<Location> QuestionnairesWXM;
        private readonly UserProfile profile;
        private readonly Settings settings;
        private readonly List<ContentTemplate> templates;
        readonly HTTPWrapper hTTPWrapper;

        public ReportCreator(IConfigurationRoot configuration, ApplicationLog applog, string WXMBearer, ViaMongoDB v)
        {
            Configuration = configuration;
            log = applog;

            via = v;

            WXM_BASE_URL = Configuration["WXM_BASE_URL"];

            hTTPWrapper = new HTTPWrapper();

            string q = InvitationsMemoryCache.GetInstance().GetActiveQuestionsFromMemoryCache(WXMBearer, hTTPWrapper);
            if (!string.IsNullOrEmpty(q))
                questions = JsonConvert.DeserializeObject<List<Question>>(q);

            string questionnaires = InvitationsMemoryCache.GetInstance().GetQuestionnaireFromMemoryCache(WXMBearer, hTTPWrapper);
            if (!string.IsNullOrEmpty(questionnaires))
                QuestionnairesWXM = JsonConvert.DeserializeObject<List<Location>>(questionnaires);

            string p = InvitationsMemoryCache.GetInstance().GetUserProfileFromMemoryCache(WXMBearer, hTTPWrapper);
            if (!string.IsNullOrEmpty(p))
                profile = JsonConvert.DeserializeObject<UserProfile>(p);

            string s = InvitationsMemoryCache.GetInstance().GetSettingsFromMemoryCache(WXMBearer, hTTPWrapper);
            if (!string.IsNullOrEmpty(s))
                settings = JsonConvert.DeserializeObject<Settings>(s);

            string t = InvitationsMemoryCache.GetInstance().GetContentTemplatesFromMemoryCache(WXMBearer, hTTPWrapper);
            if (!string.IsNullOrEmpty(t))
                templates = JsonConvert.DeserializeObject<List<ContentTemplate>>(t);
        }

        public async Task<Tuple<byte[], bool>> GetOperationMetricsReport(FilterBy filter, bool Logs = false, int skiplogs = 0, int limitlogs = 0)
        {
            if (filter == null)
                return null;

            try
            {
                if (questions == null || QuestionnairesWXM == null || profile == null || settings == null || templates == null)
                    return null;

                AccountConfiguration a = await via.GetAccountConfiguration();

                int TimeZoneOffset = (int)(profile.TimeZoneOffset == null ? settings.TimeZoneOffset : profile.TimeZoneOffset);

                string UTCTZD = TimeZoneOffset >= 0 ? "UTC+" : "UTC-";
                UTCTZD = UTCTZD + Math.Abs(Convert.ToInt32(TimeZoneOffset / 60)).ToString() + ":" + Math.Abs(TimeZoneOffset % 60).ToString();

                var package = new ExcelPackage();

                if (Logs)
                {
                    int row = 0;

                    var sheet = package.Workbook.Worksheets.Add("Detailed Log");

                    sheet.Cells[1, 1].Value = "Date Range: " + filter.afterdate.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD + " - " + filter.beforedate.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD;
                    sheet.Cells[1, 1].Style.Font.Bold = true;

                    sheet.Cells[2, 1].Value = "DeliveryWorkFlowId";
                    sheet.Cells[2, 1].Style.Font.Bold = true;
                    sheet.Cells[2, 2].Value = "TimeStamp";
                    sheet.Cells[2, 2].Style.Font.Bold = true;
                    sheet.Cells[2, 3].Value = "Questionnaire";
                    sheet.Cells[2, 3].Style.Font.Bold = true;
                    sheet.Cells[2, 4].Value = "Channel";
                    sheet.Cells[2, 4].Style.Font.Bold = true;
                    sheet.Cells[2, 5].Value = "Action";
                    sheet.Cells[2, 5].Style.Font.Bold = true;
                    sheet.Cells[2, 5].AddComment("Possible values for action: \r\n" +
                                                  "Unsubscribe- User has clicked on unsubscribe \r\n" +
                                                  "Unsubscribed- User has already unsubscribed from getting survey invites \r\n" +
                                                  "Bounced- User did not receive invite as it was bounced \r\n" +
                                                  "Exception- User did not receive invite due to an error \r\n" +
                                                  "Displayed- User clicked on the survey link and it was displayed \r\n" +
                                                  "Sent- Invite was sent to the user \r\n" +
                                                  "Throttled- User did not receive invite due to the throttling logic \r\n" +
                                                  "Answered- User answered the survey \r\n" +
                                                  "Requested- Token creation has been requested \r\n" +
                                                  "Rejected- Token creation has been rejected \r\n" +
                                                  "Tokencreated- Survey token was created for the user to answer the survey \r\n" +
                                                  "Error- User did not receive invite due to an error \r\n" +
                                                  "Supressed- User did not receive invite as it was supressed \r\n" +
                                                  "DispatchSuccessful- Invite was dispatched successfully to the user \r\n" +
                                                  "DispatchUnsuccessful- Invite was not dispatched to the user due to some error", "WXM Team");
                    sheet.Cells[2, 5].Comment.AutoFit = true;
                    sheet.Cells[2, 6].Value = "Message";
                    sheet.Cells[2, 6].Style.Font.Bold = true;
                    sheet.Cells[2, 7].Value = "DispatchID";
                    sheet.Cells[2, 7].Style.Font.Bold = true;
                    sheet.Cells[2, 8].Value = "TargetHashed";
                    sheet.Cells[2, 8].Style.Font.Bold = true;
                    sheet.Cells[2, 9].Value = "Message Sequence";
                    sheet.Cells[2, 9].Style.Font.Bold = true;
                    sheet.Cells[2, 10].Value = "Message template";
                    sheet.Cells[2, 10].Style.Font.Bold = true;
                    sheet.Cells[2, 11].Value = "Token ID";
                    sheet.Cells[2, 11].Style.Font.Bold = true;

                    FormatHeader(sheet.Cells["A2:K2"], 3);

                    row = 3;

                    List<WXMPartnerMerged> MergedData = await via.GetMergedDataFromDb(filter, skiplogs, limitlogs);

                    if (MergedData != null)
                    {
                        var Result = CreateDetailedLogs(sheet, MergedData, filter, a, row);

                        sheet = Result.Item1;

                        if (sheet == null)
                            return new Tuple<byte[], bool>(null, true);

                        return new Tuple<byte[], bool>(package.GetAsByteArray(), true);
                    }
                    else
                    {
                        return new Tuple<byte[], bool>(null, true);
                    }
                }

                List<RequestInitiatorRecords> BatchIdToFileName = await via.GetRequestInitiatorRecords();

                List<AggregatedSplits> AggregdateData = await AggregateDataForReports(filter, BatchIdToFileName, a);

                Question ZoneQuestion = questions.Where(x => x.QuestionTags.Contains("cc_zone"))?.FirstOrDefault();
                Question TouchPointQuestion = questions.Where(x => x.QuestionTags.Contains("cc_touchpoint"))?.FirstOrDefault();
                Question LocationQuestion = questions.Where(x => x.QuestionTags.Contains("cc_location"))?.FirstOrDefault();

                if (AggregdateData.Where(x => x.DisplayName == "Total")?.FirstOrDefault()?.SentCount == 0)
                    return new Tuple<byte[], bool>(CreateMetricsReport(AggregdateData, filter, a, ZoneQuestion, TouchPointQuestion, LocationQuestion), false);

                return new Tuple<byte[], bool>(CreateMetricsReport(AggregdateData, filter, a, ZoneQuestion, TouchPointQuestion, LocationQuestion), true);
            }
            catch (Exception ex)
            {
                log.logMessage += $"Error generating the excel report {ex.Message}    {ex.StackTrace}";
                return null;
            }
        }

        async Task<List<AggregatedSplits>> AggregateDataForReports(FilterBy filter, List<RequestInitiatorRecords> BatchIdToFileName,  AccountConfiguration a)
        {
            if (filter == null || BatchIdToFileName == null || a == null)
                return null;

            try
            {
                long total = await via.GetMergedDataCount(filter);

                int TimeZoneOffset = (int)(profile.TimeZoneOffset == null ? settings.TimeZoneOffset : profile.TimeZoneOffset);
                string UTCTZD = TimeZoneOffset >= 0 ? "UTC+" : "UTC-";
                UTCTZD = UTCTZD + Math.Abs(Convert.ToInt32(TimeZoneOffset / 60)).ToString() + ":" + Math.Abs(TimeZoneOffset % 60).ToString();

                List<PrefillSlicing> QuestionsForSplit = a.PrefillsForSlices;

                AggregatedSplits TotalSplit = new AggregatedSplits();
                TotalSplit.id = "Total";
                List<AggregatedSplits> FileMappedSplits = new List<AggregatedSplits>();
                List<AggregatedSplits> PrefillSplits = new List<AggregatedSplits>();
                List<List<AggregatedSplits>> ChannelSplits = new List<List<AggregatedSplits>>();
                List<List<AggregatedSplits>> QuestionnaireSplits = new List<List<AggregatedSplits>>();
                List<List<AggregatedSplits>> MonthSplits = new List<List<AggregatedSplits>>();
                List<List<AggregatedSplits>> DispatchSplits = new List<List<AggregatedSplits>>();
                List<List<AggregatedSplits>> MessageTemplateSplits = new List<List<AggregatedSplits>>();
                List<List<AggregatedSplits>> SequenceSplits = new List<List<AggregatedSplits>>();
                List<List<AggregatedSplits>> ZoneSplits = new List<List<AggregatedSplits>>();
                List<List<AggregatedSplits>> TouchpointSplits = new List<List<AggregatedSplits>>();
                List<List<AggregatedSplits>> LocationSplits = new List<List<AggregatedSplits>>();

                DateTime StartDate = filter.afterdate.AddMinutes(TimeZoneOffset);
                DateTime EndDate = filter.beforedate.AddMinutes(TimeZoneOffset);

                Dictionary<string, string> ValidMonthLimits = new Dictionary<string, string>();

                if (StartDate.Month != EndDate.Month)
                {
                    for (int i = StartDate.Month; i <= EndDate.Month; i++)
                    {
                        if (i == StartDate.Month && StartDate.Day != 1)
                        {
                            ValidMonthLimits.Add(new DateTime(2015, i, 1).ToString("MMMM"),
                                new DateTime(2015, i, 1).ToString("MMMM") + " (From " + AddOrdinal(StartDate.Day) + " to " + AddOrdinal(StartDate.EndOfMonth().Day) + ")");
                        }
                        else if (i == EndDate.Month && EndDate.Day != EndDate.EndOfMonth().Day)
                        {
                            ValidMonthLimits.Add(new DateTime(2015, i, 1).ToString("MMMM"),
                                new DateTime(2015, i, 1).ToString("MMMM") + " (From " + AddOrdinal(1) + " to " + AddOrdinal(EndDate.Day) + ")");
                        }
                        else
                            ValidMonthLimits.Add(new DateTime(2015, i, 1).ToString("MMMM"),
                                new DateTime(2015, i, 1).ToString("MMMM") + " (From " + AddOrdinal(1) + " to " +
                                AddOrdinal(new DateTime(2015, i, 1).LastDayOfMonth().Day) + ")");
                    }
                }
                else
                {
                    ValidMonthLimits.Add(StartDate.ToString("MMMM"),
                        StartDate.ToString("MMMM") + " (From " + AddOrdinal(StartDate.Day) + " to " + AddOrdinal(EndDate.Day) + ")");
                }

                if (QuestionsForSplit != null && QuestionsForSplit?.Count() != 0)
                {
                    foreach (PrefillSlicing q in QuestionsForSplit)
                    {
                        if (q.DisplayType?.ToLower() == "select" && q.MultiSelect?.Count() > 0)
                        {
                            foreach (string option in q.MultiSelect)
                            {
                                AggregatedSplits prefillsplit = new AggregatedSplits();

                                prefillsplit.DisplayName = q.Note == null ? q.Text : q.Note;
                                prefillsplit.id = q.Id;

                                prefillsplit.OptionName = option;
                                prefillsplit.AnsweredCount = 0;
                                prefillsplit.BouncedCount = 0;
                                prefillsplit.CompletedCount = 0;
                                prefillsplit.ErrorCount = 0;
                                prefillsplit.ExceptionCount = 0;
                                prefillsplit.SentCount = 0;
                                prefillsplit.ThrottledCount = 0;
                                prefillsplit.UnsubscribedCount = 0;

                                PrefillSplits.Add(prefillsplit);
                            }
                        }
                    }
                }

                if (BatchIdToFileName != null && BatchIdToFileName?.Count() != 0)
                {
                    foreach (RequestInitiatorRecords record in BatchIdToFileName)
                    {
                        AggregatedSplits split = new AggregatedSplits();

                        split.id = record.BatchId;
                        split.DisplayName = record.DisplayFileName;
                        split.AnsweredCount = 0;
                        split.BouncedCount = 0;
                        split.CompletedCount = 0;
                        split.ErrorCount = 0;
                        split.ExceptionCount = 0;
                        split.SentCount = 0;
                        split.ThrottledCount = 0;
                        split.UnsubscribedCount = 0;
                        split.FilePlacedOn = record.CreatedOn.AddMinutes(TimeZoneOffset);

                        FileMappedSplits.Add(split);
                    }
                }

                //taking account of any other channel of creating tokens apart from placing file
                AggregatedSplits OtherSourceSplit = new AggregatedSplits();

                OtherSourceSplit.id = "Other Sources";
                OtherSourceSplit.DisplayName = "Other Sources";
                OtherSourceSplit.AnsweredCount = 0;
                OtherSourceSplit.BouncedCount = 0;
                OtherSourceSplit.CompletedCount = 0;
                OtherSourceSplit.ErrorCount = 0;
                OtherSourceSplit.ExceptionCount = 0;
                OtherSourceSplit.SentCount = 0;
                OtherSourceSplit.ThrottledCount = 0;
                OtherSourceSplit.UnsubscribedCount = 0;

                FileMappedSplits.Add(OtherSourceSplit);

                //take 100000 tokens at a time to not overload memory
                for (int i = 0; i < total; i = i + 100000)
                {
                    List<WXMPartnerMerged> MergedData = await via.GetMergedDataFromDb(filter, i, 100000);

                    if (MergedData == null)
                        MergedData = await via.GetMergedDataFromDb(filter, i, 100000);

                    if (MergedData == null)
                        continue;

                    DataTable dt = new DataTable();
                    dt.Clear();
                    dt.Columns.Add("Questionnaire");
                    dt.Columns.Add("Response Status");
                    dt.Columns.Add("Message Sequence");
                    dt.Columns.Add("Batch ID");
                    dt.Columns.Add("Token ID");
                    dt.Columns.Add("DeliveryWorkFlowId");
                    dt.Columns.Add("Response Timestamp");
                    dt.Columns.Add("Sent Month");
                    dt.Columns.Add("Answered Month");
                    dt.Columns.Add("Requested At");
                    dt.Columns.Add("Last Updated");
                    dt.Columns.Add("Requested");
                    dt.Columns.Add("RequestedChannel");
                    dt.Columns.Add("Token Created Status");
                    dt.Columns.Add("TokenCreatedChannel");
                    dt.Columns.Add("Sent Status");
                    dt.Columns.Add("Channel");
                    dt.Columns.Add("SentMessage");
                    dt.Columns.Add("Message Template");
                    dt.Columns.Add("Completion Status");
                    dt.Columns.Add("Rejected");
                    dt.Columns.Add("RejectedChannel");
                    dt.Columns.Add("Error Status");
                    dt.Columns.Add("ErrorChannel");
                    dt.Columns.Add("ErrorMessage");
                    dt.Columns.Add("Supressed Status");
                    dt.Columns.Add("SupressedChannel");
                    dt.Columns.Add("DispatchStatus");
                    dt.Columns.Add("DispatchStatusChannel");
                    dt.Columns.Add("DispatchStatusMessage");
                    dt.Columns.Add("Throttling Status");
                    dt.Columns.Add("Clicked Unsubscribe");
                    dt.Columns.Add("UnsubscribeChannel");
                    dt.Columns.Add("Unsubscribed Status");
                    dt.Columns.Add("Bounced Status");
                    dt.Columns.Add("BouncedChannel");
                    dt.Columns.Add("Exception Status");
                    dt.Columns.Add("ExceptionCount");
                    dt.Columns.Add("ExceptionChannel");
                    dt.Columns.Add("ExceptionMessage");
                    dt.Columns.Add("Displayed Status");
                    dt.Columns.Add("DispatchId");
                    dt.Columns.Add("TargetHashed");
                    dt.Columns.Add("RejectedMessage");

                    Question ZoneQuestion = questions.Where(x => x.QuestionTags.Contains("cc_zone"))?.FirstOrDefault();
                    Question TouchPointQuestion = questions.Where(x => x.QuestionTags.Contains("cc_touchpoint"))?.FirstOrDefault();
                    Question LocationQuestion = questions.Where(x => x.QuestionTags.Contains("cc_location"))?.FirstOrDefault();

                    dt.Columns.Add("Zone");
                    dt.Columns.Add("Touchpoint");
                    dt.Columns.Add("Location");

                    foreach(var merged in MergedData)
                    {
                        int RemindersSent = 0;

                        if (merged.Sent)
                        {
                            RemindersSent = merged.Events.Where(x => x.SentSequence != null)?.Select(x => x.SentSequence)?.Max() == null ? 0
                                : (int)merged.Events.Where(x => x.SentSequence != null)?.Select(x => x.SentSequence)?.Max(); //Convert.ToInt32(m.SentSequence.Split(" ").LastOrDefault()) - 1;
                        }

                        List<int> ExceptionSequences = new List<int>();

                        DateTime? LastSentTime = merged.Events.Where(x => x.SentSequence == RemindersSent &&
                                                x.Action?.ToLower() == "sent")?.FirstOrDefault()?.TimeStamp;

                        if (LastSentTime == null)
                        {
                            RemindersSent = merged.Events.Where(x => x.Action?.ToLower() == "exception")?.Count() == null ? 0 :
                                                merged.Events.Where(x => x.Action?.ToLower() == "exception").Count() == 0 ? 0 :
                                                merged.Events.Where(x => x.Action?.ToLower() == "exception").Count() - 1;

                        }
                        else
                        {
                            var ExceptionAfterSent = merged.Events.Where(x => x.Action?.ToLower() == "exception" && x.TimeStamp > LastSentTime);

                            if (ExceptionAfterSent != null && ExceptionAfterSent?.Count() > 0)
                            {
                                RemindersSent = RemindersSent + ExceptionAfterSent.Count(); //starts from 0
                            }
                        }

                        if (merged.Exception)
                        {
                            for (int j = 0; j <= RemindersSent; j++)
                            {
                                if (merged.Events.Where(x => x.SentSequence == j && x.Action?.ToLower() == "sent")?.FirstOrDefault() == null)
                                {
                                    ExceptionSequences.Add(j);
                                }
                            }
                        }

                        for (int j = 0; j <= RemindersSent; j++)
                        {
                            try
                            {
                                DataRow row = dt.NewRow();

                                if (QuestionnairesWXM.Where(x => x.Name == merged.Questionnaire)?.Count() > 0)
                                    row["Questionnaire"] = QuestionnairesWXM.Where(x => x.Name == merged.Questionnaire)?.FirstOrDefault().DisplayName + " (" + merged.Questionnaire + ")";
                                else
                                    row["Questionnaire"] = merged.Questionnaire + " (Questionnaire not present)";
                                row["Response Status"] = j == RemindersSent && merged.Answered ? "Answered" :
                                    merged.Events?.Where(x => x.Action?.ToLower()?.Contains("sent") == true &&
                                    x.SentSequence == j)?.FirstOrDefault() == null ? "Not Sent" : "Unanswered";
                                row["Batch ID"] = merged.BatchId;
                                row["Token ID"] = merged._id;
                                row["DeliveryWorkFlowId"] = merged.DeliveryWorkFlowId;
                                row["Response Timestamp"] = merged.AnsweredAt.Year == 0001 ? null : j == RemindersSent && merged.Answered ? merged.AnsweredAt.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD : null;

                                string SentMonth = merged.Events?.Where(x => x.Action?.ToLower()?.Contains("sent") == true
                                    && x.SentSequence == j)?.FirstOrDefault() == null ? "Not Sent" :
                                    "Sent in " + merged.Events?.Where(x => x.Action?.ToLower()?.Contains("sent") == true
                                    && x.SentSequence == j)?.FirstOrDefault()?.TimeStamp.AddMinutes(TimeZoneOffset).ToString("MMMM");


                                string AnsweredMonth = j == RemindersSent && merged.Answered ? "Answered in " + merged.AnsweredAt.AddMinutes(TimeZoneOffset).ToString("MMMM") : "Unanswered";

                                if (ValidMonthLimits?.Keys?.Contains(SentMonth) == true)
                                    SentMonth = ValidMonthLimits[SentMonth];

                                row["Sent Month"] = SentMonth;
                                row["Answered Month"] = AnsweredMonth;

                                row["Requested At"] = merged.CreatedAt.Year == 0001 ? null : merged.CreatedAt.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD;
                                row["Last Updated"] = merged.LastUpdated.Year == 0001 ? null : merged.LastUpdated.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD;
                                row["Requested"] = merged.Requested ? "Requested" : "Not Requested";
                                row["RequestedChannel"] = merged.RequestedChannel;
                                row["Token Created Status"] = merged.TokenCreated ? "Token Created" : "Token Not Created";
                                row["TokenCreatedChannel"] = merged.TokenCreatedChannel;
                                row["Sent Status"] = merged.Events?.Where(x => x.Action?.ToLower()?.Contains("sent") == true &&
                                x.SentSequence == j)?.FirstOrDefault() == null ? "Not Sent" : "Sent";
                                row["Channel"] = merged.Sent ? merged.Events?.Where(x => x.Action?.ToLower()?.Contains("sent") == true &&
                                x.SentSequence == j)?.FirstOrDefault() == null ? "Not Sent" : merged.Events?.Where(x => x.Action?.ToLower()?.Contains("sent") == true &&
                                x.SentSequence == j)?.FirstOrDefault()?.Channel?.Split(":")?.FirstOrDefault() : "Not Sent";
                                row["SentMessage"] = merged.Sent ? merged.Events?.Where(x => x.Action?.ToLower()?.Contains("sent") == true &&
                                x.SentSequence == j)?.FirstOrDefault() == null ? "Not Sent" : merged.Events?.Where(x => x.Action?.ToLower()?.Contains("sent") == true &&
                                x.SentSequence == j)?.FirstOrDefault()?.Message : "Not Sent";

                                string TemplateId = merged.Sent ? merged.Events?.Where(x => x.Action?.ToLower()?.Contains("sent") == true &&
                                x.SentSequence == j)?.FirstOrDefault() == null ? "Not Sent" : merged.Events?.Where(x => x.Action?.ToLower()?.Contains("sent") == true &&
                                x.SentSequence == j)?.FirstOrDefault()?.MessageTemplate : "Not Sent";

                                string TemplateName = templates?.Where(x => x.Id == TemplateId)?.FirstOrDefault()?.Name;

                                string messagetemplate = null;

                                if (string.IsNullOrEmpty(TemplateName) && TemplateId == "Not Sent")
                                    messagetemplate = TemplateId;
                                else
                                {
                                    if (string.IsNullOrEmpty(TemplateName))
                                    {
                                        messagetemplate = TemplateId + " (Template not present)";
                                    }
                                    else
                                    {
                                        messagetemplate = TemplateName + " (" + TemplateId + ")";
                                    }
                                }

                                row["Message Template"] = messagetemplate;
                                row["Completion Status"] = j == RemindersSent ? merged.Partial ? "Partial" : merged.Answered ? "Completed" : "Unanswered" : "Unanswered";
                                row["Rejected"] = merged.Rejected ? "Rejected" : "Not Rejected";
                                row["RejectedChannel"] = merged.RejectedChannel;
                                row["RejectedMessage"] = merged.RejectedMessage;
                                row["Error Status"] = merged.Error ? "Error" : "No Error";
                                row["ErrorChannel"] = merged.ErrorChannel;
                                row["ErrorMessage"] = merged.ErrorMessage;
                                row["Supressed Status"] = merged.Supressed ? "Supressed" : "Not Supressed";
                                row["SupressedChannel"] = merged.SupressedChannel;

                                row["DispatchStatus"] = merged.Events?.Where(x => x.Action?.ToLower()?
                                .Contains("dispatchsuccessful") == true &&
                                j.ToString() == x.Message?.Split("=")?.LastOrDefault())?.FirstOrDefault() != null
                                ? "Successful" : merged.Events?.Where(x => x.Action?.ToLower()?
                                .Contains("dispatchunsuccessful") == true &&
                                j.ToString() == x.Message?.Split("=")?.LastOrDefault())?.FirstOrDefault() != null ? "Unsuccessful"
                                : "Unsuccessful";
                                row["DispatchStatusChannel"] = merged.Events?.Where(x => (x.Action?.ToLower()?
                                .Contains("dispatchsuccessful") == true || x.Action?.ToLower()?
                                .Contains("dispatchunsuccessful") == true) &&
                                j.ToString() == x.Message?.Split("=")?.LastOrDefault())?.FirstOrDefault()?.Channel;
                                row["DispatchStatusMessage"] = merged.Events?.Where(x => (x.Action?.ToLower()?
                                .Contains("dispatchsuccessful") == true || x.Action?.ToLower()?
                                .Contains("dispatchunsuccessful") == true) &&
                                j.ToString() == x.Message?.Split("=")?.LastOrDefault())?.FirstOrDefault()?.LogMessage;

                                row["Throttling Status"] = merged.Throttled ? "Throttled" : "Not Throttled";
                                if (a.DispatchChannels.Where(x => x.DispatchId == merged.DispatchId)?.Count() > 0)
                                    row["DispatchId"] = a.DispatchChannels.Where(x => x.DispatchId == merged.DispatchId).FirstOrDefault().DispatchName + " (" + merged.DispatchId + ")";
                                else
                                    row["DispatchId"] = merged.DispatchId + " (Dispatch not present)";
                                row["TargetHashed"] = merged.TargetHashed;
                                row["Clicked Unsubscribe"] = merged.Unsubscribe ? "Yes" : "No";
                                row["UnsubscribeChannel"] = merged.UnsubscribeChannel;
                                row["Unsubscribed Status"] = merged.Unsubscribed ? "Unsubscribed" : "Not Unsubscribed";
                                row["Bounced Status"] = merged.Bounced ? "Bounced" : "Not Bounced";
                                row["BouncedChannel"] = merged.BouncedChannel;
                                row["Exception Status"] = ExceptionSequences?.Contains(j) == true ?
                                    "Exception" : "No Exception";
                                row["ExceptionMessage"] = ExceptionSequences?.Contains(j) == true ?
                                    merged.Events.Where(x => x.Action?.ToLower()?.Contains("exception") == true)?.ToList()[ExceptionSequences.IndexOf(j)]?.Message :
                                    null;
                                row["ExceptionCount"] = merged.ExceptionCount;
                                row["ExceptionChannel"] = ExceptionSequences?.Contains(j) == true ?
                                    merged.Events.Where(x => x.Action?.ToLower()?.Contains("exception") == true)?.ToList()[ExceptionSequences.IndexOf(j)]?.Channel :
                                    null;
                                row["Displayed Status"] = merged.Displayed ? "Displayed" : "Not Displayed";
                                row["Message Sequence"] = j == 0 ? "Message 1" : "Message " + (j + 1).ToString();

                                if (merged.Responses?.Any(x => x.QuestionId == ZoneQuestion?.Id) == true)
                                    row["Zone"] = merged.Responses.Where(x => x.QuestionId == ZoneQuestion?.Id).FirstOrDefault().TextInput;
                                if (merged.Responses?.Any(x => x.QuestionId == TouchPointQuestion?.Id) == true)
                                    row["Touchpoint"] = merged.Responses.Where(x => x.QuestionId == TouchPointQuestion?.Id).FirstOrDefault().TextInput;
                                if (merged.Responses?.Any(x => x.QuestionId == LocationQuestion?.Id) == true)
                                    row["Location"] = merged.Responses.Where(x => x.QuestionId == LocationQuestion?.Id).FirstOrDefault().TextInput;

                                dt.Rows.Add(row);
                            }
                            catch (Exception ex)
                            {
                                continue;
                            }
                        }

                        if (merged.Answered)
                        {
                            TotalSplit.AnsweredCount++;
                            foreach (AggregatedSplits s in PrefillSplits.Where(x => x.OptionName != null &&
                                                                            merged.Responses?.Where(z => z.QuestionId == x.id)?.FirstOrDefault()?.TextInput == x.OptionName))
                            {
                                s.AnsweredCount++;
                            }
                            if (FileMappedSplits.Where(x => x.id == merged.BatchId)?.Count() == 1)
                                FileMappedSplits.Where(x => x.id == merged.BatchId).FirstOrDefault().AnsweredCount++;
                            else
                                FileMappedSplits.Where(x => x.id == "Other Sources").FirstOrDefault().AnsweredCount++;
                        }
                        if (merged.Bounced && !merged.Sent)
                        {
                            TotalSplit.BouncedCount++;
                            foreach (AggregatedSplits s in PrefillSplits.Where(x => x.OptionName != null &&
                                                                            merged.Responses?.Where(z => z.QuestionId == x.id)?.FirstOrDefault()?.TextInput == x.OptionName))
                            {
                                s.BouncedCount++;
                            }
                            if (FileMappedSplits.Where(x => x.id == merged.BatchId)?.Count() == 1)
                                FileMappedSplits.Where(x => x.id == merged.BatchId).FirstOrDefault().BouncedCount++;
                            else
                                FileMappedSplits.Where(x => x.id == "Other Sources").FirstOrDefault().BouncedCount++;
                        }
                        if (!merged.Partial && merged.Answered)
                        {
                            TotalSplit.CompletedCount++;

                            foreach (AggregatedSplits s in PrefillSplits.Where(x => x.OptionName != null &&
                                                                        merged.Responses?.Where(z => z.QuestionId == x.id)?.FirstOrDefault()?.TextInput == x.OptionName))
                            {
                                s.CompletedCount++;
                            }
                            if (FileMappedSplits.Where(x => x.id == merged.BatchId)?.Count() == 1)
                                FileMappedSplits.Where(x => x.id == merged.BatchId).FirstOrDefault().CompletedCount++;
                            else
                                FileMappedSplits.Where(x => x.id == "Other Sources").FirstOrDefault().CompletedCount++;
                        }
                        if (merged.Error && !merged.Sent)
                        {
                            TotalSplit.ErrorCount++;
                            foreach (AggregatedSplits s in PrefillSplits.Where(x => x.OptionName != null &&
                                                                        merged.Responses?.Where(z => z.QuestionId == x.id)?.FirstOrDefault()?.TextInput == x.OptionName))
                            {
                                s.ErrorCount++;
                            }
                            if (FileMappedSplits.Where(x => x.id == merged.BatchId)?.Count() == 1)
                                FileMappedSplits.Where(x => x.id == merged.BatchId).FirstOrDefault().ErrorCount++;
                            else
                                FileMappedSplits.Where(x => x.id == "Other Sources").FirstOrDefault().ErrorCount++;
                        }
                        if (merged.Exception && !merged.Sent)
                        {
                            TotalSplit.ExceptionCount++;
                            foreach (AggregatedSplits s in PrefillSplits.Where(x => x.OptionName != null &&
                                                                        merged.Responses?.Where(z => z.QuestionId == x.id)?.FirstOrDefault()?.TextInput == x.OptionName))
                            {
                                s.ExceptionCount++;
                            }
                            if (FileMappedSplits.Where(x => x.id == merged.BatchId)?.Count() == 1)
                                FileMappedSplits.Where(x => x.id == merged.BatchId).FirstOrDefault().ExceptionCount++;
                            else
                                FileMappedSplits.Where(x => x.id == "Other Sources").FirstOrDefault().ExceptionCount++;
                        }
                        if (merged.Sent)
                        {
                            TotalSplit.SentCount++;
                            foreach (AggregatedSplits s in PrefillSplits.Where(x => x.OptionName != null &&
                                                                               merged.Responses?.Where(z => z.QuestionId == x.id)?.FirstOrDefault()?.TextInput == x.OptionName))
                            {
                                s.SentCount++;
                            }
                            if (FileMappedSplits.Where(x => x.id == merged.BatchId)?.Count() == 1)
                                FileMappedSplits.Where(x => x.id == merged.BatchId).FirstOrDefault().SentCount++;
                            else
                                FileMappedSplits.Where(x => x.id == "Other Sources").FirstOrDefault().SentCount++;
                        }
                        if (merged.Throttled && !merged.Sent)
                        {
                            TotalSplit.ThrottledCount++;
                            foreach (AggregatedSplits s in PrefillSplits.Where(x => x.OptionName != null &&
                                                                            merged.Responses?.Where(z => z.QuestionId == x.id)?.FirstOrDefault()?.TextInput == x.OptionName))
                            {
                                s.ThrottledCount++;
                            }
                            if (FileMappedSplits.Where(x => x.id == merged.BatchId)?.Count() == 1)
                                FileMappedSplits.Where(x => x.id == merged.BatchId).FirstOrDefault().ThrottledCount++;
                            else
                                FileMappedSplits.Where(x => x.id == "Other Sources").FirstOrDefault().ThrottledCount++;
                        }
                        if (merged.Unsubscribed && !merged.Sent)
                        {
                            TotalSplit.UnsubscribedCount++;
                            foreach (AggregatedSplits s in PrefillSplits.Where(x => x.OptionName != null &&
                                                                            merged.Responses?.Where(z => z.QuestionId == x.id)?.FirstOrDefault()?.TextInput == x.OptionName))
                            {
                                s.UnsubscribedCount++;
                            }
                            if (FileMappedSplits.Where(x => x.id == merged.BatchId)?.Count() == 1)
                                FileMappedSplits.Where(x => x.id == merged.BatchId).FirstOrDefault().UnsubscribedCount++;
                            else
                                FileMappedSplits.Where(x => x.id == "Other Sources").FirstOrDefault().UnsubscribedCount++;
                        }
                    }

                    DataTable SentData = new DataTable();

                    try
                    {
                        SentData = dt.AsEnumerable().
                        Where(r => r.Field<string>("Sent Status") == "Sent").
                        OrderByDescending(y => y.Field<String>("Questionnaire")).
                        CopyToDataTable();
                    }
                    catch
                    {
                        continue;
                    }

                    Parallel.Invoke(() => {
                        //Channel splits
                        List<string> UniqueVals = SentData.AsEnumerable().Select(x => x["Channel"]?.ToString())?.Distinct()?.ToList();

                        if (UniqueVals?.Count() > 0)
                        {
                            List<AggregatedSplits> splits = Splitter(SentData, "Channel", UniqueVals);

                            if (splits?.Count() > 0)
                                ChannelSplits.Add(splits);
                        }
                    }, 
                    () => {
                        //Questionnaire splits
                        List<string> UniqueVals = SentData.AsEnumerable().Select(x => x["Questionnaire"]?.ToString())?.Distinct()?.ToList();

                        if (UniqueVals?.Count() > 0)
                        {
                            List<AggregatedSplits> splits = Splitter(SentData, "Questionnaire", UniqueVals);

                            if (splits?.Count() > 0)
                                QuestionnaireSplits.Add(splits);
                        }
                    }, 
                    () => {
                        //Month splits
                        List<string> UniqueVals = SentData.AsEnumerable().Select(x => x["Sent Month"]?.ToString())?.Distinct()?.ToList();

                        if (UniqueVals?.Count() > 0)
                        {
                            List<AggregatedSplits> splits = Splitter(SentData, "Sent Month", UniqueVals);

                            if (splits?.Count() > 0)
                                MonthSplits.Add(splits);
                        }
                    },
                    () => {
                        //Dispatch splits
                        List<string> UniqueVals = SentData.AsEnumerable().Select(x => x["DispatchId"]?.ToString())?.Distinct()?.ToList();

                        if (UniqueVals?.Count() > 0)
                        {
                            List<AggregatedSplits> splits = Splitter(SentData, "DispatchId", UniqueVals);

                            if (splits?.Count() > 0)
                                DispatchSplits.Add(splits);
                        }
                    },
                    () => {
                        //Message template splits
                        List<string> UniqueVals = SentData.AsEnumerable().Select(x => x["Message Template"]?.ToString())?.Distinct()?.ToList();

                        if (UniqueVals?.Count() > 0)
                        {
                            List<AggregatedSplits> splits = Splitter(SentData, "Message Template", UniqueVals);

                            if (splits?.Count() > 0)
                                MessageTemplateSplits.Add(splits);
                        }
                    },
                    () => {
                        //Sequence splits
                        List<string> UniqueVals = SentData.AsEnumerable().Select(x => x["Message Sequence"]?.ToString())?.Distinct()?.ToList();

                        if (UniqueVals?.Count() > 0)
                        {
                            List<AggregatedSplits> splits = Splitter(SentData, "Message Sequence", UniqueVals);

                            if (splits?.Count() > 0)
                                SequenceSplits.Add(splits);
                        }
                    },
                    () => {
                        //Zone Splits
                        if (ZoneQuestion != null)
                        {
                            List<string> UniqueVals = SentData.AsEnumerable().Select(x => x["Zone"]?.ToString())?.Distinct()?.ToList();

                            if (UniqueVals?.Count() > 0)
                            {
                                List<AggregatedSplits> splits = Splitter(SentData, "Zone", UniqueVals);

                                if (splits?.Count() > 0)
                                    ZoneSplits.Add(splits);
                            }
                        }
                    },
                    () => {
                        //touch point splits
                        if (TouchPointQuestion != null)
                        {
                            List<string> UniqueVals = SentData.AsEnumerable().Select(x => x["Touchpoint"]?.ToString())?.Distinct()?.ToList();

                            if (UniqueVals?.Count() > 0)
                            {
                                List<AggregatedSplits> splits = Splitter(SentData, "Touchpoint", UniqueVals);

                                if (splits?.Count() > 0)
                                    TouchpointSplits.Add(splits);
                            }
                        }
                    },
                    () => {
                        // location splits
                        if (LocationQuestion != null)
                        {
                            List<string> UniqueVals = SentData.AsEnumerable().Select(x => x["Location"]?.ToString())?.Distinct()?.ToList();

                            if (UniqueVals?.Count() > 0)
                            {
                                List<AggregatedSplits> splits = Splitter(SentData, "Location", UniqueVals);

                                if (splits?.Count() > 0)
                                    LocationSplits.Add(splits);
                            }
                        }
                    });
                }

                List<AggregatedSplits> FinalSplits = new List<AggregatedSplits>();

                FinalSplits.Add(TotalSplit);
                FinalSplits.AddRange(FileMappedSplits);
                FinalSplits.AddRange(PrefillSplits);

                List<string> GetUniques(List<List<AggregatedSplits>> Aggregates)
                {
                    if (Aggregates?.Count() > 0)
                    {
                        List<string> Uniques = new List<string>();

                        foreach (List<AggregatedSplits> splits in Aggregates)
                        {
                            if(splits != null && splits.Count() > 0)
                                Uniques.AddRange(splits.Select(x => x.DisplayName));
                        }

                        return Uniques.Distinct().ToList();
                    }

                    return null;
                }

                List<AggregatedSplits> AggregateToFinalSplits(List<List<AggregatedSplits>> DividedSplits, string id)
                {
                    try
                    {
                        if (DividedSplits.Where(x => x != null)?.Count() > 0)
                        {
                            List<AggregatedSplits> FinalSplits = new List<AggregatedSplits>();

                            List<string> Uniques = GetUniques(DividedSplits);

                            foreach (string unique in Uniques)
                            {
                                try
                                {
                                    AggregatedSplits totalsplit = new AggregatedSplits();
                                    totalsplit.id = id;
                                    totalsplit.DisplayName = unique;

                                    var requiredsplits = DividedSplits.Where(z => z?.Select(t => t.DisplayName)
                                                            ?.Contains(unique) == true);

                                    totalsplit.SentCount = requiredsplits.Select(x => x.Where(y => y.DisplayName == unique)
                                                            .FirstOrDefault().SentCount).Sum();

                                    totalsplit.AnsweredCount = requiredsplits.Select(x => x.Where(y => y.DisplayName == unique)
                                                            .FirstOrDefault().AnsweredCount).Sum();

                                    totalsplit.CompletedCount = requiredsplits.Select(x => x.Where(y => y.DisplayName == unique)
                                                            .FirstOrDefault().CompletedCount).Sum();

                                    FinalSplits.Add(totalsplit);
                                }
                                catch (Exception ex)
                                {
                                    continue;
                                }
                            }

                            return FinalSplits;
                        }

                        return null;
                    }
                    catch (Exception ex)
                    {
                        log.logMessage += $"Error in AggregateToFinalSplits method {ex.Message}    {ex.StackTrace}";
                        return null;
                    }
                }

                var final = AggregateToFinalSplits(ChannelSplits, "Channel");
                if(final != null)
                    FinalSplits.AddRange(final);

                final = AggregateToFinalSplits(QuestionnaireSplits, "Questionnaire");
                if (final != null)
                    FinalSplits.AddRange(final);

                final = AggregateToFinalSplits(MonthSplits, "Sent Month");
                if (final != null)
                    FinalSplits.AddRange(final);

                final = AggregateToFinalSplits(DispatchSplits, "DispatchId");
                if (final != null)
                    FinalSplits.AddRange(final);

                final = AggregateToFinalSplits(MessageTemplateSplits, "Message Template");
                if (final != null)
                    FinalSplits.AddRange(final);

                final = AggregateToFinalSplits(SequenceSplits, "Message Sequence");
                if (final != null)
                    FinalSplits.AddRange(final);

                final = AggregateToFinalSplits(ZoneSplits, "Zone");
                if (final != null)
                    FinalSplits.AddRange(final);

                final = AggregateToFinalSplits(TouchpointSplits, "Touchpoint");
                if (final != null)
                    FinalSplits.AddRange(final);

                final = AggregateToFinalSplits(LocationSplits, "Location");
                if (final != null)
                    FinalSplits.AddRange(final);

                return FinalSplits;
            }
            catch (Exception ex)
            {
                log.logMessage += $"Error aggregating data for excel report {ex.Message}    {ex.StackTrace}";
                return null;
            }
        }

        List<AggregatedSplits> Splitter(DataTable dt, string header, List<string> UniqueVals)
        {
            if (dt == null || header == null)
                return null;

            if (!dt?.Columns?.Contains(header) == true)
                return null;

            List<AggregatedSplits> splits = new List<AggregatedSplits>();

            try
            {
                Dictionary<string, Tuple<int, int>> stats = new Dictionary<string, Tuple<int, int>>();

                foreach (string val in UniqueVals.Where(x => !string.IsNullOrEmpty(x)))
                {
                    AggregatedSplits a = new AggregatedSplits();

                    a.id = header;
                    a.DisplayName = val;

                    a.CompletedCount = dt.AsEnumerable().Where(r => r.Field<String>(header) == val
                                            && r.Field<String>("Response Status") == "Answered" &&
                                            r.Field<String>("Completion Status") == "Completed").Count();
                    a.AnsweredCount = dt.AsEnumerable().Where(r => r.Field<String>(header) == val
                                            && r.Field<String>("Response Status") == "Answered").Count();
                    a.SentCount = dt.AsEnumerable().Where(r => r.Field<String>(header) == val
                                            && r.Field<string>("Sent Status") == "Sent").Count();

                    splits.Add(a);
                }

                return splits;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        Tuple<ExcelWorksheet, int> CreateDetailedLogs(ExcelWorksheet sheet, List<WXMPartnerMerged> MergedData, FilterBy filter, AccountConfiguration a, int RowNo)
        {
            if (MergedData == null || filter == null || a == null)
                return null;

            try
            {
                #region Detailed Logs

                int TimeZoneOffset = (int)(profile.TimeZoneOffset == null ? settings.TimeZoneOffset : profile.TimeZoneOffset);
                string UTCTZD = TimeZoneOffset >= 0 ? "UTC+" : "UTC-";
                UTCTZD = UTCTZD + Math.Abs(Convert.ToInt32(TimeZoneOffset / 60)).ToString() + ":" + Math.Abs(TimeZoneOffset % 60).ToString();

                foreach (WXMPartnerMerged o in MergedData)
                {
                    try
                    {
                        foreach (DeliveryEvent d in o.Events)
                        {
                            if (d.Action == "Sent")
                            {
                                sheet = DoDefaultValues(sheet, o, RowNo, a);
                                sheet.Cells[RowNo, 4].Value = d.Channel;
                                sheet.Cells[RowNo, 5].Value = "Sent";
                                sheet.Cells[RowNo, 6].Value = d.Message;

                                string TemplateName = templates?.Where(x => x.Id == d.MessageTemplate)?.FirstOrDefault()?.Name;

                                string messagetemplate = null;

                                if (string.IsNullOrEmpty(TemplateName) && d.MessageTemplate == "Not Sent")
                                    messagetemplate = d.MessageTemplate;
                                else
                                {
                                    if (string.IsNullOrEmpty(TemplateName))
                                    {
                                        messagetemplate = d.MessageTemplate + " (Template not present)";
                                    }
                                    else
                                    {
                                        messagetemplate = TemplateName + " (" + d.MessageTemplate + ")";
                                    }
                                }

                                sheet.Cells[RowNo, 10].Value = messagetemplate;
                                sheet.Cells[RowNo, 9].Value = d.SentSequence == 0 ? "Message 1" : d.SentSequence != null ? "Message " + (d.SentSequence + 1)?.ToString() : null;
                                sheet.Cells[RowNo, 2].Value = d.TimeStamp.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD;

                                RowNo++;
                            }
                            else
                            {
                                sheet = DoDefaultValues(sheet, o, RowNo, a);
                                sheet.Cells[RowNo, 4].Value = d.Channel;
                                sheet.Cells[RowNo, 5].Value = d.Action;
                                //in case of dispatch status, need to add log message
                                sheet.Cells[RowNo, 6].Value = d.Action?.ToLower() == "dispatchsuccessful" ||
                                    d.Action?.ToLower() == "dispatchunsuccessful" ?
                                    d.LogMessage : d.Message;
                                sheet.Cells[RowNo, 2].Value = d.TimeStamp.Year == 0001 ? o.CreatedAt.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD :
                                d.TimeStamp.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD;

                                RowNo++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log.logMessage += $"Error in adding a log to the detailed logs excel sheet {ex.Message}    {ex.StackTrace}";
                        continue;
                    }

                }

                sheet.Cells["A2:G2"].AutoFitColumns(10, 60);

                return new Tuple<ExcelWorksheet, int>(sheet, RowNo);

                #endregion
            }
            catch (Exception ex)
            {
                log.logMessage += $"Error generating detailed logs report {ex.Message}    {ex.StackTrace}";
                return null;
            }
        }

        ExcelWorksheet DoDefaultValues(ExcelWorksheet sheet, WXMPartnerMerged data, int row, AccountConfiguration a)
        {
            sheet.Cells[row, 1].Value = data.DeliveryWorkFlowId;
            if (QuestionnairesWXM.Where(x => x.Name == data.Questionnaire)?.Count() > 0)
                sheet.Cells[row, 3].Value = QuestionnairesWXM.Where(x => x.Name == data.Questionnaire)?.FirstOrDefault().DisplayName + " (" + data.Questionnaire + ")";
            else
                sheet.Cells[row, 3].Value = data.Questionnaire + " (Questionnaire not present)";
            sheet.Cells[row, 11].Value = data._id;
            if (a.DispatchChannels.Where(x => x.DispatchId == data.DispatchId)?.Count() > 0)
                sheet.Cells[row, 7].Value = a.DispatchChannels.Where(x => x.DispatchId == data.DispatchId).FirstOrDefault().DispatchName + " (" + data.DispatchId + ")";
            else
                sheet.Cells[row, 7].Value = data.DispatchId + " (Dispatch not present)";

            sheet.Cells[row, 8].Value = data.TargetHashed;

            return sheet;
        }

        byte[] CreateMetricsReport(List<AggregatedSplits> AllSplits, FilterBy filter, AccountConfiguration a, Question ZoneQuestion, Question TouchPointQuestion, Question LocationQuestion)
        {
            if (AllSplits == null || filter == null || a == null)
                return null;

            try
            {
                ExcelWorksheet MakeOverviewHeaders(ExcelWorksheet sheet, int startrow)
                {
                    sheet.Column(1).Width = 45;

                    sheet.Cells[startrow, 2].Value = "Total Invites Requested";
                    sheet.Column(9).Width = 16;
                    sheet.Cells[startrow, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[startrow, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(79, 129, 189));
                    sheet.Cells[startrow, 2].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));
                    sheet.Cells[startrow, 2, startrow, 9].Style.Font.Bold = true;
                    sheet.Cells[startrow, 2, startrow, 9].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
                    sheet.Cells[startrow, 2, startrow, 9].Style.WrapText = true;

                    sheet.Cells[startrow, 3].Value = "Throttled";
                    sheet.Column(5).Width = 16;
                    sheet.Cells[startrow, 4].Value = "Unsubscribed";
                    sheet.Column(6).Width = 16;
                    sheet.Cells[startrow, 5].Value = "Bounced";
                    sheet.Column(7).Width = 16;
                    sheet.Cells[startrow, 6].Value = "Exception";
                    sheet.Column(8).Width = 16;

                    sheet.Cells[startrow, 3, startrow, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[startrow, 3, startrow, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(192, 0, 0));
                    sheet.Cells[startrow, 3, startrow, 6].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));
                    sheet.Cells[startrow, 3, startrow, 6].Style.Font.Bold = true;
                    sheet.Cells[startrow, 3, startrow, 6].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
                    sheet.Cells[startrow, 3, startrow, 6].Style.WrapText = true;

                    sheet.Cells[startrow, 7].Value = "Total Invites Processed";
                    sheet.Column(2).Width = 16;
                    sheet.Cells[startrow, 8].Value = "Total Invites Answered(Out of Total Processed)";
                    sheet.Column(3).Width = 16;
                    sheet.Cells[startrow, 9].Value = "Completed Responses(Out of Total Answered)";
                    sheet.Column(4).Width = 16;

                    sheet.Cells[startrow, 7, startrow, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[startrow, 7, startrow, 9].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(84, 130, 53));
                    sheet.Cells[startrow, 7, startrow, 9].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));
                    sheet.Cells[startrow, 7, startrow, 9].Style.Font.Bold = true;
                    sheet.Cells[startrow, 7, startrow, 9].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
                    sheet.Cells[startrow, 7, startrow, 9].Style.WrapText = true;

                    sheet.Row(startrow).Height = 45;

                    return sheet;
                }

                var package = new ExcelPackage();

                int TimeZoneOffset = (int)(profile.TimeZoneOffset == null ? settings.TimeZoneOffset : profile.TimeZoneOffset);
                string UTCTZD = TimeZoneOffset >= 0 ? "UTC+" : "UTC-";
                UTCTZD = UTCTZD + Math.Abs(Convert.ToInt32(TimeZoneOffset / 60)).ToString() + ":" + Math.Abs(TimeZoneOffset % 60).ToString();

                #region Overview sheet

                var OverviewSheet = package.Workbook.Worksheets.Add("Overview");

                OverviewSheet.Cells[1, 1, 1, 8].Merge = true;
                OverviewSheet.Cells[1, 1, 1, 8].Value = "Overall Performance Report";
                OverviewSheet.Cells[1, 1, 1, 8].Style.Font.Bold = true;
                FormatHeader(OverviewSheet.Cells[1, 1, 1, 8], 2);
                OverviewSheet.Cells[2, 1, 2, 8].Merge = true;
                OverviewSheet.Cells[2, 1, 2, 8].Value = "Date Range: " + filter.afterdate.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD + " - " + filter.beforedate.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD;
                FormatHeader(OverviewSheet.Cells[2, 1, 2, 8], 4);

                OverviewSheet = MakeOverviewHeaders(OverviewSheet, 4);

                int SentCount = AllSplits.Where(x => x.id == "Total")?.Count() != 0 ?
                                AllSplits.Where(x => x.id == "Total").FirstOrDefault().SentCount : 0;
                int ThrottledCount = AllSplits.Where(x => x.id == "Total")?.Count() != 0 ?
                                AllSplits.Where(x => x.id == "Total").FirstOrDefault().ThrottledCount : 0;
                int UnsubscribedCount = AllSplits.Where(x => x.id == "Total")?.Count() != 0 ?
                                AllSplits.Where(x => x.id == "Total").FirstOrDefault().UnsubscribedCount : 0;
                int BouncedCount = AllSplits.Where(x => x.id == "Total")?.Count() != 0 ?
                                AllSplits.Where(x => x.id == "Total").FirstOrDefault().BouncedCount : 0;
                int ExceptionCount = AllSplits.Where(x => x.id == "Total")?.Count() != 0 ?
                                AllSplits.Where(x => x.id == "Total").FirstOrDefault().ExceptionCount : 0;
                int AnsweredCount = AllSplits.Where(x => x.id == "Total")?.Count() != 0 ?
                                AllSplits.Where(x => x.id == "Total").FirstOrDefault().AnsweredCount : 0;
                int CompletedCount = AllSplits.Where(x => x.id == "Total")?.Count() != 0 ?
                                AllSplits.Where(x => x.id == "Total").FirstOrDefault().CompletedCount : 0;

                int total = SentCount + ThrottledCount + UnsubscribedCount + BouncedCount + ExceptionCount;

                OverviewSheet.Cells[5, 2].Value = SentCount + ThrottledCount + UnsubscribedCount + BouncedCount + ExceptionCount;
                OverviewSheet.Cells[5, 3].Value = ThrottledCount;
                OverviewSheet.Cells[5, 4].Value = UnsubscribedCount;
                OverviewSheet.Cells[5, 5].Value = BouncedCount;
                OverviewSheet.Cells[5, 6].Value = ExceptionCount;
                OverviewSheet.Cells[5, 7].Value = SentCount;
                OverviewSheet.Cells[5, 8].Value = AnsweredCount;
                OverviewSheet.Cells[5, 9].Value = CompletedCount;

                if (total != 0)
                {
                    OverviewSheet.Cells[6, 7].Value = (double)SentCount / total;

                    if (SentCount != 0)
                        OverviewSheet.Cells[6, 8].Value = (double)AnsweredCount / SentCount;
                    else
                    {
                        OverviewSheet.Cells[6, 8].Value = "NA";
                        OverviewSheet.Cells[6, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }

                    if (AnsweredCount != 0)
                        OverviewSheet.Cells[6, 9].Value = (double)CompletedCount / AnsweredCount;
                    else
                    {
                        OverviewSheet.Cells[6, 9].Value = "NA";
                        OverviewSheet.Cells[6, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }

                    OverviewSheet.Cells[6, 3].Value = (double)ThrottledCount / total;
                    OverviewSheet.Cells[6, 4].Value = (double)UnsubscribedCount / total;
                    OverviewSheet.Cells[6, 5].Value = (double)BouncedCount / total;
                    OverviewSheet.Cells[6, 6].Value = (double)ExceptionCount / total;
                    OverviewSheet.Cells[6, 2].Value = (double)(SentCount + ThrottledCount + UnsubscribedCount + BouncedCount + ExceptionCount) / total;
                }
                else
                {
                    OverviewSheet.Cells[6, 7].Value = "NA";
                    OverviewSheet.Cells[6, 8].Value = "NA";
                    OverviewSheet.Cells[6, 9].Value = "NA";
                    OverviewSheet.Cells[6, 3].Value = "NA";
                    OverviewSheet.Cells[6, 4].Value = "NA";
                    OverviewSheet.Cells[6, 5].Value = "NA";
                    OverviewSheet.Cells[6, 6].Value = "NA";
                    OverviewSheet.Cells[6, 2].Value = "NA";

                    OverviewSheet.Cells[6, 2, 6, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }

                OverviewSheet.Cells[6, 2, 6, 9].Style.Numberformat.Format = "#0.00%";

                OverviewSheet.Cells[5, 1].Value = "Total Count";
                OverviewSheet.Cells[5, 1].Style.Font.Bold = true;
                OverviewSheet.Cells[6, 1].Value = "Total Percentage";
                OverviewSheet.Cells[6, 1].Style.Font.Bold = true;
                OverviewSheet.Cells[5, 1, 6, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                OverviewSheet.Cells[5, 1, 6, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(79, 129, 189));
                OverviewSheet.Cells[5, 1, 6, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));

                int r = 8;

                OverviewSheet.Cells[r, 1, r, 8].Merge = true;
                OverviewSheet.Cells[r, 1, r, 8].Value = "Overall Performance Split by Data Source";
                OverviewSheet.Cells[r, 1, r, 8].Style.Font.Bold = true;
                FormatHeader(OverviewSheet.Cells[r, 1, r, 8], 2);

                r++;

                OverviewSheet = MakeOverviewHeaders(OverviewSheet, r);

                r++;

                var FileSplits = AllSplits.Where(x => x.DisplayName?.Contains(".xlsx") == true || x.DisplayName?.Contains(".csv") == true);

                if (FileSplits?.Count() > 0)
                {
                    foreach (AggregatedSplits q in FileSplits)
                    {
                        if (q.SentCount + q.ThrottledCount + q.UnsubscribedCount + q.BouncedCount + q.ExceptionCount != 0)
                        {
                            OverviewSheet.Cells[r, 1, r + 1, 1].Merge = true;
                            OverviewSheet.Cells[r, 1, r + 1, 1].Value = q.DisplayName + " (" + q.FilePlacedOn.ToString("dd/MM/yyyy h:mm tt") + ")";
                            OverviewSheet.Cells[r, 1, r + 1, 1].Style.Font.Bold = true;
                            OverviewSheet.Cells[r, 1, r + 1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            OverviewSheet.Cells[r, 1, r + 1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 242, 204));
                            OverviewSheet.Cells[r, 1, r + 1, 1].Style.WrapText = true;
                            OverviewSheet.Cells[r, 1, r + 1, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                            OverviewSheet.Cells[r, 1, r + 1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                            OverviewSheet.Cells[r, 2].Value = q.SentCount + q.ThrottledCount + q.UnsubscribedCount + q.BouncedCount + q.ExceptionCount;
                            OverviewSheet.Cells[r, 3].Value = q.ThrottledCount;
                            OverviewSheet.Cells[r, 4].Value = q.UnsubscribedCount;
                            OverviewSheet.Cells[r, 5].Value = q.BouncedCount;
                            OverviewSheet.Cells[r, 6].Value = q.ExceptionCount;
                            OverviewSheet.Cells[r, 7].Value = q.SentCount;
                            OverviewSheet.Cells[r, 8].Value = q.AnsweredCount;
                            OverviewSheet.Cells[r, 9].Value = q.CompletedCount;

                            int t = q.SentCount + q.ThrottledCount + q.UnsubscribedCount + q.BouncedCount + q.ExceptionCount;

                            OverviewSheet.Cells[r + 1, 2, r + 1, 9].Style.Numberformat.Format = "#0.00%";

                            OverviewSheet.Cells[r + 1, 7].Value = (double)q.SentCount / t;

                            if (q.SentCount != 0)
                                OverviewSheet.Cells[r + 1, 8].Value = (double)q.AnsweredCount / q.SentCount;
                            else
                            {
                                OverviewSheet.Cells[r + 1, 8].Value = "NA";
                                OverviewSheet.Cells[r + 1, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                            }

                            if (q.AnsweredCount != 0)
                                OverviewSheet.Cells[r + 1, 9].Value = (double)q.CompletedCount / q.AnsweredCount;
                            else
                            {
                                OverviewSheet.Cells[r + 1, 9].Value = "NA";
                                OverviewSheet.Cells[r + 1, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                            }

                            OverviewSheet.Cells[r + 1, 3].Value = (double)q.ThrottledCount / t;
                            OverviewSheet.Cells[r + 1, 4].Value = (double)q.UnsubscribedCount / t;
                            OverviewSheet.Cells[r + 1, 5].Value = (double)q.BouncedCount / t;
                            OverviewSheet.Cells[r + 1, 6].Value = (double)q.ExceptionCount / t;
                            OverviewSheet.Cells[r + 1, 2].Value = (double)(q.SentCount + q.ThrottledCount + q.UnsubscribedCount + q.BouncedCount + q.ExceptionCount) / t;

                            r++;
                            r++;
                        }
                    }
                }

                var OtherSources = AllSplits.Where(x => x.id == "Other Sources")?.FirstOrDefault();

                int OtherSourcesCount = 0;

                if (OtherSources != null)
                {
                    OtherSourcesCount = OtherSources.SentCount + OtherSources.ThrottledCount + OtherSources.UnsubscribedCount +
                        OtherSources.BouncedCount + OtherSources.ExceptionCount;
                }

                if (OtherSourcesCount > 0)
                {
                    OverviewSheet.Cells[r, 1, r + 1, 1].Merge = true;
                    OverviewSheet.Cells[r, 1, r + 1, 1].Value = OtherSources.DisplayName;
                    OverviewSheet.Cells[r, 1, r + 1, 1].Style.Font.Bold = true;
                    OverviewSheet.Cells[r, 1, r + 1, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(0, 0, 0));
                    OverviewSheet.Cells[r, 1, r + 1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    OverviewSheet.Cells[r, 1, r + 1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 242, 204));
                    OverviewSheet.Cells[r, 1, r + 1, 1].Style.WrapText = true;
                    OverviewSheet.Cells[r, 1, r + 1, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    OverviewSheet.Cells[r, 1, r + 1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    OverviewSheet.Cells[r, 2].Value = OtherSourcesCount;
                    OverviewSheet.Cells[r, 3].Value = OtherSources.ThrottledCount;
                    OverviewSheet.Cells[r, 4].Value = OtherSources.UnsubscribedCount;
                    OverviewSheet.Cells[r, 5].Value = OtherSources.BouncedCount;
                    OverviewSheet.Cells[r, 6].Value = OtherSources.ExceptionCount;
                    OverviewSheet.Cells[r, 7].Value = OtherSources.SentCount;
                    OverviewSheet.Cells[r, 8].Value = OtherSources.AnsweredCount;
                    OverviewSheet.Cells[r, 9].Value = OtherSources.CompletedCount;

                    OverviewSheet.Cells[r + 1, 2, r + 1, 9].Style.Numberformat.Format = "#0.00%";

                    OverviewSheet.Cells[r + 1, 7].Value = (double)OtherSources.SentCount / OtherSourcesCount;

                    if (OtherSources.SentCount != 0)
                        OverviewSheet.Cells[r + 1, 8].Value = (double)OtherSources.AnsweredCount / OtherSources.SentCount;
                    else
                    {
                        OverviewSheet.Cells[r + 1, 8].Value = "NA";
                        OverviewSheet.Cells[r + 1, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }

                    if (OtherSources.AnsweredCount != 0)
                        OverviewSheet.Cells[r + 1, 9].Value = (double)OtherSources.CompletedCount / OtherSources.AnsweredCount;
                    else
                    {
                        OverviewSheet.Cells[r + 1, 9].Value = "NA";
                        OverviewSheet.Cells[r + 1, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }

                    OverviewSheet.Cells[r + 1, 3].Value = (double)OtherSources.ThrottledCount / OtherSourcesCount;
                    OverviewSheet.Cells[r + 1, 4].Value = (double)OtherSources.UnsubscribedCount / OtherSourcesCount;
                    OverviewSheet.Cells[r + 1, 5].Value = (double)OtherSources.BouncedCount / OtherSourcesCount;
                    OverviewSheet.Cells[r + 1, 6].Value = (double)OtherSources.ExceptionCount / OtherSourcesCount;
                    OverviewSheet.Cells[r + 1, 2].Value = (double)(OtherSources.SentCount + OtherSources.ThrottledCount + OtherSources.UnsubscribedCount + OtherSources.BouncedCount + OtherSources.ExceptionCount) / OtherSourcesCount;

                    r++;
                    r++;
                }

                var AllSourcesMapped = AllSplits.Where(x => x.DisplayName?.Contains(".xlsx") == true || x.DisplayName?.Contains("Other Sources") == true || x.DisplayName?.Contains(".csv") == true);

                if (AllSourcesMapped != null)
                {
                    SentCount = AllSourcesMapped.Select(x => x.SentCount).Sum();
                    ThrottledCount = AllSourcesMapped.Select(x => x.ThrottledCount).Sum();
                    UnsubscribedCount = AllSourcesMapped.Select(x => x.UnsubscribedCount).Sum();
                    BouncedCount = AllSourcesMapped.Select(x => x.BouncedCount).Sum();
                    ExceptionCount = AllSourcesMapped.Select(x => x.ExceptionCount).Sum();
                    AnsweredCount = AllSourcesMapped.Select(x => x.AnsweredCount).Sum();
                    CompletedCount = AllSourcesMapped.Select(x => x.CompletedCount).Sum();

                    total = SentCount + ThrottledCount + UnsubscribedCount +
                    BouncedCount + ExceptionCount;

                    OverviewSheet.Cells[r, 2].Value = total;
                    OverviewSheet.Cells[r, 3].Value = ThrottledCount;
                    OverviewSheet.Cells[r, 4].Value = UnsubscribedCount;
                    OverviewSheet.Cells[r, 5].Value = BouncedCount;
                    OverviewSheet.Cells[r, 6].Value = ExceptionCount;
                    OverviewSheet.Cells[r, 7].Value = SentCount;
                    OverviewSheet.Cells[r, 8].Value = AnsweredCount;
                    OverviewSheet.Cells[r, 9].Value = CompletedCount;

                    if (total != 0)
                    {
                        OverviewSheet.Cells[r + 1, 7].Value = (double)SentCount / total;

                        if (SentCount != 0)
                            OverviewSheet.Cells[r + 1, 8].Value = (double)AnsweredCount / SentCount;
                        else
                        {
                            OverviewSheet.Cells[r + 1, 8].Value = "NA";
                            OverviewSheet.Cells[r + 1, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                        }

                        if (AnsweredCount != 0)
                            OverviewSheet.Cells[r + 1, 9].Value = (double)CompletedCount / AnsweredCount;
                        else
                        {
                            OverviewSheet.Cells[r + 1, 9].Value = "NA";
                            OverviewSheet.Cells[r + 1, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                        }

                        OverviewSheet.Cells[r + 1, 3].Value = (double)ThrottledCount / total;
                        OverviewSheet.Cells[r + 1, 4].Value = (double)UnsubscribedCount / total;
                        OverviewSheet.Cells[r + 1, 5].Value = (double)BouncedCount / total;
                        OverviewSheet.Cells[r + 1, 6].Value = (double)ExceptionCount / total;
                        OverviewSheet.Cells[r + 1, 2].Value = (double)(SentCount + ThrottledCount + UnsubscribedCount + BouncedCount + ExceptionCount) / total;
                    }
                    else
                    {
                        OverviewSheet.Cells[r + 1, 7].Value = "NA";
                        OverviewSheet.Cells[r + 1, 8].Value = "NA";
                        OverviewSheet.Cells[r + 1, 9].Value = "NA";
                        OverviewSheet.Cells[r + 1, 3].Value = "NA";
                        OverviewSheet.Cells[r + 1, 4].Value = "NA";
                        OverviewSheet.Cells[r + 1, 5].Value = "NA";
                        OverviewSheet.Cells[r + 1, 6].Value = "NA";
                        OverviewSheet.Cells[r + 1, 2].Value = "NA";

                        OverviewSheet.Cells[r + 1, 2, r + 1, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }

                    OverviewSheet.Cells[r + 1, 2, r + 1, 9].Style.Numberformat.Format = "#0.00%";

                    OverviewSheet.Cells[r, 1].Value = "Total Count";
                    OverviewSheet.Cells[r, 1].Style.Font.Bold = true;
                    OverviewSheet.Cells[r + 1, 1].Value = "Total Percentage";
                    OverviewSheet.Cells[r + 1, 1].Style.Font.Bold = true;
                    OverviewSheet.Cells[r, 1, r + 1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    OverviewSheet.Cells[r, 1, r + 1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(79, 129, 189));
                    OverviewSheet.Cells[r, 1, r + 1, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));

                }

                #endregion

                #region tables

                List<PrefillSlicing> QuestionsForSplit = a.PrefillsForSlices;

                if (QuestionsForSplit != null && QuestionsForSplit?.Count() > 0)
                {
                    var QuestionsSplitSheet = package.Workbook.Worksheets.Add("Split by questions");

                    QuestionsSplitSheet.Cells[1, 1, 1, 8].Merge = true;
                    QuestionsSplitSheet.Cells[1, 1, 1, 8].Value = "Prefill question based performance report";
                    QuestionsSplitSheet.Cells[1, 1, 1, 8].Style.Font.Bold = true;
                    FormatHeader(QuestionsSplitSheet.Cells[1, 1, 1, 8], 2);
                    QuestionsSplitSheet.Cells[2, 1, 2, 8].Merge = true;
                    QuestionsSplitSheet.Cells[2, 1, 2, 8].Value = "Date Range: " + filter.afterdate.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD + " - " + filter.beforedate.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD;
                    FormatHeader(QuestionsSplitSheet.Cells[2, 1, 2, 8], 4);

                    QuestionsSplitSheet.Column(1).Width = 44;

                    r = 4;

                    foreach (PrefillSlicing q in QuestionsForSplit)
                    {
                        string header = "Overall Performance Split by ";

                        if (q.Note == null)
                            header = header + q.Text;
                        else
                            header = header + q.Note;

                        QuestionsSplitSheet.Cells[r, 1, r, 9].Merge = true;
                        QuestionsSplitSheet.Cells[r, 1, r, 9].Value = header;
                        QuestionsSplitSheet.Cells[r, 1, r, 9].Style.Font.Bold = true;
                        FormatHeader(QuestionsSplitSheet.Cells[r, 1, r, 9], 2);
                        r++;

                        QuestionsSplitSheet = MakeOverviewHeaders(QuestionsSplitSheet, r);

                        r++;

                        foreach (AggregatedSplits s in AllSplits.Where(x => x.id == q.Id))
                        {
                            QuestionsSplitSheet.Cells[r + 1, 2, r + 1, 9].Style.Numberformat.Format = "#0.00%";

                            QuestionsSplitSheet.Cells[r, 1, r + 1, 1].Merge = true;
                            QuestionsSplitSheet.Cells[r, 1, r + 1, 1].Value = s.OptionName;
                            QuestionsSplitSheet.Cells[r, 1, r + 1, 1].Style.Font.Bold = true;
                            QuestionsSplitSheet.Cells[r, 1, r + 1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            QuestionsSplitSheet.Cells[r, 1, r + 1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 242, 204));
                            QuestionsSplitSheet.Cells[r, 1, r + 1, 1].Style.WrapText = true;
                            QuestionsSplitSheet.Cells[r, 1, r + 1, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                            QuestionsSplitSheet.Cells[r, 1, r + 1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                            QuestionsSplitSheet.Cells[r, 2].Value = s.SentCount + s.ThrottledCount + s.UnsubscribedCount + s.BouncedCount + s.ExceptionCount;
                            QuestionsSplitSheet.Cells[r, 3].Value = s.ThrottledCount;
                            QuestionsSplitSheet.Cells[r, 4].Value = s.UnsubscribedCount;
                            QuestionsSplitSheet.Cells[r, 5].Value = s.BouncedCount;
                            QuestionsSplitSheet.Cells[r, 6].Value = s.ExceptionCount;
                            QuestionsSplitSheet.Cells[r, 7].Value = s.SentCount;
                            QuestionsSplitSheet.Cells[r, 8].Value = s.AnsweredCount;
                            QuestionsSplitSheet.Cells[r, 9].Value = s.CompletedCount;

                            if (s.SentCount + s.ThrottledCount + s.UnsubscribedCount + s.BouncedCount + s.ExceptionCount != 0)
                            {
                                total = s.SentCount + s.ThrottledCount + s.UnsubscribedCount + s.BouncedCount + s.ExceptionCount;

                                QuestionsSplitSheet.Cells[r + 1, 7].Value = (double)s.SentCount / total;

                                if (s.SentCount != 0)
                                    QuestionsSplitSheet.Cells[r + 1, 8].Value = (double)s.AnsweredCount / s.SentCount;
                                else
                                {
                                    QuestionsSplitSheet.Cells[r + 1, 8].Value = "NA";
                                    QuestionsSplitSheet.Cells[r + 1, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                                }

                                if (s.AnsweredCount != 0)
                                    QuestionsSplitSheet.Cells[r + 1, 9].Value = (double)s.CompletedCount / s.AnsweredCount;
                                else
                                {
                                    QuestionsSplitSheet.Cells[r + 1, 9].Value = "NA";
                                    QuestionsSplitSheet.Cells[r + 1, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                                }

                                //QuestionsSplitSheet.Cells[r + 1, 8].Value = s.SentCount != 0 ? Convert.ToString(Math.Round((double)s.AnsweredCount / s.SentCount, 4) * 100) + "%" : "NA";
                                //QuestionsSplitSheet.Cells[r + 1, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                                //QuestionsSplitSheet.Cells[r + 1, 9].Value = s.AnsweredCount != 0 ? Convert.ToString(Math.Round((double)s.CompletedCount / s.AnsweredCount, 4) * 100) + "%" : "NA";
                                //QuestionsSplitSheet.Cells[r + 1, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                                QuestionsSplitSheet.Cells[r + 1, 3].Value = (double)s.ThrottledCount / total;
                                QuestionsSplitSheet.Cells[r + 1, 4].Value = (double)s.UnsubscribedCount / total;
                                QuestionsSplitSheet.Cells[r + 1, 5].Value = (double)s.BouncedCount / total;
                                QuestionsSplitSheet.Cells[r + 1, 6].Value = (double)s.ExceptionCount / total;
                                QuestionsSplitSheet.Cells[r + 1, 2].Value = (double)(s.SentCount + s.ThrottledCount + s.UnsubscribedCount + s.BouncedCount + s.ExceptionCount) / total;
                            }
                            else
                            {
                                QuestionsSplitSheet.Cells[r + 1, 7].Value = "NA";
                                QuestionsSplitSheet.Cells[r + 1, 8].Value = "NA";
                                QuestionsSplitSheet.Cells[r + 1, 9].Value = "NA";
                                QuestionsSplitSheet.Cells[r + 1, 3].Value = "NA";
                                QuestionsSplitSheet.Cells[r + 1, 4].Value = "NA";
                                QuestionsSplitSheet.Cells[r + 1, 5].Value = "NA";
                                QuestionsSplitSheet.Cells[r + 1, 6].Value = "NA";
                                QuestionsSplitSheet.Cells[r + 1, 2].Value = "NA";

                                QuestionsSplitSheet.Cells[r + 1, 2, r + 1, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                            }

                            r++;
                            r++;
                        }

                        SentCount = AllSplits.Where(x => x.id == q.Id).Select(y => y.SentCount).Sum();
                        ThrottledCount = AllSplits.Where(x => x.id == q.Id).Select(y => y.ThrottledCount).Sum();
                        UnsubscribedCount = AllSplits.Where(x => x.id == q.Id).Select(y => y.UnsubscribedCount).Sum();
                        BouncedCount = AllSplits.Where(x => x.id == q.Id).Select(y => y.BouncedCount).Sum();
                        ExceptionCount = AllSplits.Where(x => x.id == q.Id).Select(y => y.ExceptionCount).Sum();
                        AnsweredCount = AllSplits.Where(x => x.id == q.Id).Select(y => y.AnsweredCount).Sum();
                        CompletedCount = AllSplits.Where(x => x.id == q.Id).Select(y => y.CompletedCount).Sum();

                        total = SentCount + ThrottledCount + UnsubscribedCount + BouncedCount + ExceptionCount;

                        QuestionsSplitSheet.Cells[r, 2].Value = SentCount + ThrottledCount + UnsubscribedCount + BouncedCount + ExceptionCount;
                        QuestionsSplitSheet.Cells[r, 3].Value = ThrottledCount;
                        QuestionsSplitSheet.Cells[r, 4].Value = UnsubscribedCount;
                        QuestionsSplitSheet.Cells[r, 5].Value = BouncedCount;
                        QuestionsSplitSheet.Cells[r, 6].Value = ExceptionCount;
                        QuestionsSplitSheet.Cells[r, 7].Value = SentCount;
                        QuestionsSplitSheet.Cells[r, 8].Value = AnsweredCount;
                        QuestionsSplitSheet.Cells[r, 9].Value = CompletedCount;

                        if (total != 0)
                        {
                            QuestionsSplitSheet.Cells[r + 1, 7].Value = (double)SentCount / total;

                            if (SentCount != 0)
                                QuestionsSplitSheet.Cells[r + 1, 8].Value = (double)AnsweredCount / SentCount;
                            else
                            {
                                QuestionsSplitSheet.Cells[r + 1, 8].Value = "NA";
                                QuestionsSplitSheet.Cells[r + 1, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                            }

                            if (AnsweredCount != 0)
                                QuestionsSplitSheet.Cells[r + 1, 9].Value = (double)CompletedCount / AnsweredCount;
                            else
                            {
                                QuestionsSplitSheet.Cells[r + 1, 9].Value = "NA";
                                QuestionsSplitSheet.Cells[r + 1, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                            }

                            QuestionsSplitSheet.Cells[r + 1, 3].Value = (double)ThrottledCount / total;
                            QuestionsSplitSheet.Cells[r + 1, 4].Value = (double)UnsubscribedCount / total;
                            QuestionsSplitSheet.Cells[r + 1, 5].Value = (double)BouncedCount / total;
                            QuestionsSplitSheet.Cells[r + 1, 6].Value = (double)ExceptionCount / total;
                            QuestionsSplitSheet.Cells[r + 1, 2].Value = (double)(SentCount + ThrottledCount + UnsubscribedCount + BouncedCount + ExceptionCount) / total;

                            QuestionsSplitSheet.Cells[r + 1, 2, r + 1, 9].Style.Numberformat.Format = "#0.00%";
                        }
                        else
                        {
                            QuestionsSplitSheet.Cells[r + 1, 7].Value = "NA";
                            QuestionsSplitSheet.Cells[r + 1, 8].Value = "NA";
                            QuestionsSplitSheet.Cells[r + 1, 9].Value = "NA";
                            QuestionsSplitSheet.Cells[r + 1, 3].Value = "NA";
                            QuestionsSplitSheet.Cells[r + 1, 4].Value = "NA";
                            QuestionsSplitSheet.Cells[r + 1, 5].Value = "NA";
                            QuestionsSplitSheet.Cells[r + 1, 6].Value = "NA";
                            QuestionsSplitSheet.Cells[r + 1, 2].Value = "NA";

                            QuestionsSplitSheet.Cells[r + 1, 2, r + 1, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                        }

                        QuestionsSplitSheet.Cells[r, 1].Value = "Total Count";
                        QuestionsSplitSheet.Column(1).Width = 21;
                        QuestionsSplitSheet.Cells[r, 1].Style.Font.Bold = true;
                        QuestionsSplitSheet.Cells[r + 1, 1].Value = "Total Percentage";
                        QuestionsSplitSheet.Cells[r + 1, 1].Style.Font.Bold = true;
                        QuestionsSplitSheet.Cells[r, 1, r + 1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        QuestionsSplitSheet.Cells[r, 1, r + 1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(79, 129, 189));
                        QuestionsSplitSheet.Cells[r, 1, r + 1, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));

                        r++;
                        r++;
                        r++;
                    }
                }

                var channel = package.Workbook.Worksheets.Add("Split by Channel");
                channel = CreateTable(channel, AllSplits, "Channel", filter, TimeZoneOffset, UTCTZD);

                var QuestionnairesSplit = package.Workbook.Worksheets.Add("Split by Questionnaires");
                QuestionnairesSplit = CreateTable(QuestionnairesSplit, AllSplits, "Questionnaire", filter, TimeZoneOffset, UTCTZD);

                var MonthSplit = package.Workbook.Worksheets.Add("Split by Month");
                MonthSplit = CreateTable(MonthSplit, AllSplits, "Sent Month", filter, TimeZoneOffset, UTCTZD);

                var DispatchSplit = package.Workbook.Worksheets.Add("Split by Dispatch");
                DispatchSplit = CreateTable(DispatchSplit, AllSplits, "DispatchId", filter, TimeZoneOffset, UTCTZD);

                var TemplateSplit = package.Workbook.Worksheets.Add("Split by Message Template");
                TemplateSplit = CreateTable(TemplateSplit, AllSplits, "Message Template", filter, TimeZoneOffset, UTCTZD);

                var SequenceSplit = package.Workbook.Worksheets.Add("Split by Sent Sequence");
                SequenceSplit = CreateTable(SequenceSplit, AllSplits, "Message Sequence", filter, TimeZoneOffset, UTCTZD);

                if (ZoneQuestion != null && AllSplits.Where(x => x.id == "Zone")?.Count() > 0)
                {
                    var ZoneSplit = package.Workbook.Worksheets.Add("Split by Zone");
                    ZoneSplit = CreateTable(ZoneSplit, AllSplits, "Zone", filter, TimeZoneOffset, UTCTZD);
                }
                if (TouchPointQuestion != null && AllSplits.Where(x => x.id == "Touchpoint")?.Count() > 0)
                {
                    var TouchpointSplit = package.Workbook.Worksheets.Add("Split by Touchpoint");
                    TouchpointSplit = CreateTable(TouchpointSplit, AllSplits, "Touchpoint", filter, TimeZoneOffset, UTCTZD);
                }
                if (LocationQuestion != null && AllSplits.Where(x => x.id == "Location")?.Count() > 0)
                {
                    var LocationSplit = package.Workbook.Worksheets.Add("Split by Location");
                    LocationSplit = CreateTable(LocationSplit, AllSplits, "Location", filter, TimeZoneOffset, UTCTZD);
                }

                #endregion

                return package.GetAsByteArray();
            }
            catch(Exception ex)
            {
                log.logMessage += $"Error generating metrics report {ex.Message}    {ex.StackTrace}";
                return null;
            }
        }

        ExcelWorksheet CreateTable(ExcelWorksheet e, List<AggregatedSplits> splits, string PivotColumn, FilterBy filter, int TimeZoneOffset, string UTCTZD)
        {
            if (splits == null || PivotColumn == null || filter == null || UTCTZD == null)
                return null;
            
            if (splits.Select(x => x.id).Contains(PivotColumn) == false)
                return null;

            try
            {
                List<string> UniqueVals = splits.Where(x => x.id == PivotColumn).Select(y => y.DisplayName)?.Distinct().ToList();

                Dictionary<string, Tuple<int, int>> stats = new Dictionary<string, Tuple<int, int>>();

                e.Cells[1, 1, 1, 8].Merge = true;
                e.Cells[1, 1, 1, 8].Value = PivotColumn + " Performance Report";
                e.Cells[1, 1, 1, 8].Style.Font.Bold = true;
                FormatHeader(e.Cells[1, 1, 1, 8], 2);

                e.Cells[2, 1, 2, 8].Merge = true;
                e.Cells[2, 1, 2, 8].Value = "Date Range: " + filter.afterdate.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD + " - " + filter.beforedate.AddMinutes(TimeZoneOffset).ToString("dd/MM/yyyy h:mm tt") + " " + UTCTZD;
                FormatHeader(e.Cells[2, 1, 2, 8], 4);

                e.Cells[7, 7, 14, 14].Merge = true;

                switch (PivotColumn)
                {
                    case "Channel":
                        e.Cells[7, 7, 14, 14].Value = "This table contains data of total invites that " +
                    "were sent during the set date range split by Channels. " +
                    "The total invites sent excludes requests that were throttled OR unsubscribed. " +
                    "The total invites sent for each channel include multiple messages sent for the " +
                    "same token as follow up messages, and the total number of invites sent may be " +
                    "more than actual unique invites (tokens) sent. \r\n" +
                    "If partial response collection is switched ON, then Answered responses will show a further split " +
                    "for Completed Responses, that will indicate the completion rates for Invites that were completely answered.";
                        break;

                    case "Sent Month":
                        e.Cells[7, 7, 14, 14].Value = "This table contains data of total invites that " +
                        "were sent during the set date range split by Months. The total invites sent " +
                        "excludes requests that were throttled OR unsubscribed. Some months in the " +
                        "selected date range may not have full month data. See table for details. \r\n" +
                        "If partial response collection is switched ON, then Answered responses will show a " +
                        "further split for Completed Responses, that will indicate the completion rates " +
                        "for Invites that were completely answered.";
                        break;

                    case "Zone":
                        e.Cells[7, 7, 14, 14].Value = "This table contains data of total invites that " +
                        "were sent during the set date range split by " + PivotColumn +
                        "The total invites sent excludes requests that were throttled OR unsubscribed. \r\n" +
                        "If partial response collection is switched ON, then Answered responses will show a " +
                        "further split for Completed Responses, that will indicate the completion rates " +
                        "for Invites that were completely answered. \r\n" +
                        "Total invites requested may differ from overall total depending on invites that have been sent with Zone as a pre-fill.";
                        break;

                    case "Touchpoint":
                        e.Cells[7, 7, 14, 14].Value = "This table contains data of total invites that " +
                        "were sent during the set date range split by " + PivotColumn +
                        "The total invites sent excludes requests that were throttled OR unsubscribed. \r\n" +
                        "If partial response collection is switched ON, then Answered responses will show a " +
                        "further split for Completed Responses, that will indicate the completion rates " +
                        "for Invites that were completely answered.\r\n" +
                        "Total invites requested may differ from overall total depending on invites that have been sent with Touchpoint as a pre-fill.";
                        break;

                    case "Location":
                        e.Cells[7, 7, 14, 14].Value = "This table contains data of total invites that " +
                        "were sent during the set date range split by " + PivotColumn +
                        "The total invites sent excludes requests that were throttled OR unsubscribed. \r\n" +
                        "If partial response collection is switched ON, then Answered responses will show a further" +
                        " split for Completed Responses, that will indicate the completion rates for " +
                        "Invites that were completely answered.\r\n" +
                        "Total invites requested may differ from overall total depending on invites that have been sent with Location as a pre - fill.";
                        break;

                    default:
                        e.Cells[7, 7, 14, 14].Value = "This table contains data of total invites that " +
                        "were sent during the set date range split by " + PivotColumn +
                        "The total invites sent excludes requests that were throttled OR unsubscribed. \r\n" +
                        "If partial response collection is switched ON, then Answered responses will show a " +
                        "further split for Completed Responses, that will indicate the completion rates " +
                        "for Invites that were completely answered.";
                        break;
                }

                e.Cells[7, 7, 14, 14].Style.WrapText = true;
                e.Cells[7, 7, 14, 14].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                e.Cells[7, 7, 14, 14].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;

                int row = 4;

                e.Column(1).Width = 44;
                e.Column(2).Width = 16;
                e.Column(3).Width = 16;
                e.Column(4).Width = 16;
                e.Column(5).Width = 16;

                e.Cells[row, 2].Value = "Total Invites Processed";
                e.Cells[row, 2].Style.Font.Bold = true;
                e.Cells[row, 2, row, 4].Style.Fill.PatternType = ExcelFillStyle.Solid;
                e.Cells[row, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(79, 129, 189));
                e.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));
                e.Cells[row, 2, row, 4].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
                e.Cells[row, 2, row, 4].Style.WrapText = true;

                e.Cells[row, 3].Value = "Total Invites Answered(Out of Total Processed)";
                e.Cells[row, 3].Style.Font.Bold = true;
                e.Cells[row, 3].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(84, 130, 53));
                e.Cells[row, 3].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));

                e.Cells[row, 4].Value = "Completed Responses(Out of Total Answered)";
                e.Cells[row, 4].Style.Font.Bold = true;
                e.Cells[row, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(84, 130, 53));
                e.Cells[row, 4].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));

                row++;

                int TotalSent = 0;
                int TotalAnswered = 0;
                int TotalCompleted = 0;

                foreach ( string val in UniqueVals.Where(x => !string.IsNullOrEmpty(x)))
                {
                    e.Cells[row, 1, row + 1, 1].Merge = true;
                    e.Cells[row, 1, row + 1, 1].Value = val;
                    e.Cells[row, 1, row + 1, 1].Style.WrapText = true;
                    FormatHeader(e.Cells[row, 1, row + 1, 1], 4);

                    int CompletedCount = splits.Where(x => x.id == PivotColumn && x.DisplayName == val) != null ?
                        splits.Where(x => x.id == PivotColumn && x.DisplayName == val).FirstOrDefault().CompletedCount :
                        0;

                    int AnsweredCount = splits.Where(x => x.id == PivotColumn && x.DisplayName == val) != null ?
                        splits.Where(x => x.id == PivotColumn && x.DisplayName == val).FirstOrDefault().AnsweredCount :
                        0;

                    int SentCount = splits.Where(x => x.id == PivotColumn && x.DisplayName == val) != null ?
                        splits.Where(x => x.id == PivotColumn && x.DisplayName == val).FirstOrDefault().SentCount :
                        0;

                    e.Cells[row, 2].Value = SentCount;
                    TotalSent = TotalSent + SentCount;

                    e.Cells[row, 3].Value = AnsweredCount;
                    TotalAnswered = TotalAnswered + AnsweredCount;

                    e.Cells[row, 4].Value = CompletedCount;
                    TotalCompleted = CompletedCount + TotalCompleted;

                    if (AnsweredCount != 0)
                        e.Cells[row + 1, 4].Value = (double)CompletedCount / AnsweredCount;
                    else
                    {
                        e.Cells[row + 1, 4].Value = "NA";
                        e.Cells[row + 1, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }

                    if (SentCount != 0)
                    {
                        e.Cells[row + 1, 3].Value = (double)AnsweredCount / SentCount;
                        e.Cells[row + 1, 2].Value = (double)SentCount / SentCount;
                    }
                    else
                    {
                        e.Cells[row + 1, 3].Value = "NA";
                        e.Cells[row + 1, 2, row + 1, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }

                    e.Cells[row + 1, 2, row + 1, 4].Style.Numberformat.Format = "#0.00%";

                    //e.Cells[row + 1, 4].Value = AnsweredCount != 0 ? Convert.ToString(Math.Round((double)CompletedCount / AnsweredCount, 4) * 100) + "%" : "NA";
                    //e.Cells[row + 1, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                    //e.Cells[row + 1, 3].Value = SentCount != 0 ? Convert.ToString(Math.Round((double)AnsweredCount / SentCount, 4) * 100) + "%" : "NA";
                    //e.Cells[row + 1, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                    //e.Cells[row + 1, 2].Value = SentCount != 0 ? Convert.ToString(Math.Round((double)SentCount / SentCount, 4) *100) + "%" : "NA";
                    //e.Cells[row + 1, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                    row++;
                    row++;
                }

                e.Cells[row, 2].Value = TotalSent;
                e.Cells[row, 3].Value = TotalAnswered;
                e.Cells[row, 4].Value = TotalCompleted;

                if (TotalSent != 0)
                {
                    e.Cells[row + 1, 2].Value = (double)TotalSent / TotalSent;
                    e.Cells[row + 1, 3].Value = (double)TotalAnswered / TotalSent;
                    if (TotalAnswered != 0)
                        e.Cells[row + 1, 4].Value = (double)TotalCompleted / TotalAnswered;
                    else
                        e.Cells[row + 1, 4].Value = "NA";

                    e.Cells[row + 1, 2, row + 1, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }
                else
                {
                    e.Cells[row + 1, 2].Value = "NA";
                    e.Cells[row + 1, 3].Value = "NA";
                    e.Cells[row + 1, 4].Value = "NA";

                    e.Cells[row + 1, 2, row + 1, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                }

                e.Cells[row + 1, 2, row + 1, 4].Style.Numberformat.Format = "#0.00%";

                e.Cells[row, 1].Value = "Total Count";
                e.Cells[row, 1].Style.Font.Bold = true;
                e.Cells[row + 1, 1].Value = "Total Percentage";
                e.Cells[row + 1, 1].Style.Font.Bold = true;
                e.Cells[row, 1, row + 1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                e.Cells[row, 1, row + 1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(79, 129, 189));
                e.Cells[row, 1, row + 1, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));

                return e;
            }
            catch (Exception ex)
            {
                log.logMessage += $"Error generating metrics report while creating table {ex.Message}    {ex.StackTrace}";
                return null;
            }
        }

        class AggregatedSplits
        {
            public string id { get; set; }
            public string DisplayName { get; set; }
            public string OptionName { get; set; }
            public int SentCount { get; set; }
            public int ThrottledCount { get; set; }
            public int UnsubscribedCount { get; set; }
            public int BouncedCount { get; set; }
            public int ExceptionCount { get; set; }
            public int AnsweredCount { get; set; }
            public int CompletedCount { get; set; }
            public int ErrorCount { get; set; }
            public DateTime FilePlacedOn { get; set; }
        }

        void FormatHeader(ExcelRange range, int type = 1)
        {
            var color_yellow = System.Drawing.Color.FromArgb(255, 242, 204);
            var color_lightBlue = System.Drawing.Color.FromArgb(79, 129, 189);
            var color_lightGray = System.Drawing.Color.FromArgb(242, 242, 242);
            var color_darkgrey = System.Drawing.Color.FromArgb(128, 128, 128);

            switch (type)
            {
                case 1: //sheet format
                    range.AutoFitColumns(10, 50);
                    range.Style.Font.Bold = true;
                    break;
                case 2: //sheet header, and date range
                    range.Style.Font.Size = 18;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    range.Style.Fill.BackgroundColor.SetColor(color_yellow);
                    break;
                case 3: //table header
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(255, 255, 255));
                    range.Style.Fill.BackgroundColor.SetColor(color_lightBlue);
                    break;
                case 4: //date range
                    range.Style.Font.Size = 12;
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    range.Style.Fill.BackgroundColor.SetColor(color_yellow);
                    break;
                case 5:
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(color_darkgrey);
                    break;
                default:
                    break;
            }

        }

        public static string AddOrdinal(int num)
        {
            if (num <= 0) return num.ToString();

            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }

            switch (num % 10)
            {
                case 1:
                    return num + "st";
                case 2:
                    return num + "nd";
                case 3:
                    return num + "rd";
                default:
                    return num + "th";
            }
        }
    }
}
