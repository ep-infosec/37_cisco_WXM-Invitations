using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XM.ID.Invitations.Net;
using XM.ID.Net;

namespace DPReporting
{
    class ReportTask
    {
        ApplicationLog log;
        IConfigurationRoot Configuration;
        SMTPServer smtpServer;
        readonly HTTPWrapper hTTPWrapper;
        ScheduleReportSettings schedule;
        ViaMongoDB via;

        public ReportTask(IConfigurationRoot configuration, ApplicationLog applog, ViaMongoDB v)
        {
            Configuration = configuration;
            log = applog;
            hTTPWrapper = new HTTPWrapper();
            via = v;
        }

        public async Task ReportSendingTask()
        {
            log.logMessage += $"Started on : {DateTime.UtcNow.ToString()} ";

            #region setup

            ConfigureSettings();

            int reportFor = schedule.ReportForLastDays;

            double hourlyDelay = schedule.Frequency;

            bool IsScheduleReport = schedule.ScheduleReport;

            DateTime StartDate = DateTime.UtcNow;

            if (IsScheduleReport)
            {

                try
                {
                    StartDate = DateTime.ParseExact(schedule.StartDate, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

                    bool autopick = schedule.AutoPickLastStartDate;

                    //modify startdate for report in case app crashes and start date is eligible to be changed through the property AutoPickLastStartDate
                    if (File.Exists(Configuration["LogFilePath"] + "/startdate.json") && autopick)
                    {
                        using (StreamReader r = new StreamReader(Configuration["LogFilePath"] + "/startdate.json"))
                        {
                            string json = r.ReadToEnd();
                            List<Dictionary<string, string>> items = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json);
                            StartDate = DateTime.ParseExact(items[0]["StartDate"], "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.logMessage += "StartDate needs to be in this format- yyyy-MM-ddTHH:mm:ss. needs to be a valid startdate";
                    log.logMessage += $"Error in report task {ex.Message}    {ex.StackTrace}";
                    log.AddLogsToFile(DateTime.UtcNow);
                    return;
                }

                if (reportFor == 0 || hourlyDelay == 0)
                {
                    log.logMessage += " ReportForLastDays and Frequency needs to be a number";
                    log.AddLogsToFile(DateTime.UtcNow);
                    return;
                }
            }

            AccountConfiguration a = await via.GetAccountConfiguration();

            var sendOutReport = SetUpReportSender(a);            

            #endregion

            try
            {
                await RunReportTask(StartDate, reportFor, hourlyDelay, sendOutReport, a);
            }
            catch(Exception ex)
            {
                log.logMessage += $"Error in report task {ex.Message}    {ex.StackTrace}";
                return;
            }
        }

        public void ConfigureSettings()
        {
            try
            {
                bool.TryParse(Configuration["ScheduleReport:IsScheduleReport"], out bool IsSchedule);
                int.TryParse(Configuration["ScheduleReport:ReportForLastDays"], out int reportFor);
                double.TryParse(Configuration["ScheduleReport:Frequency"], out double hourlyDelay);
                bool.TryParse(Configuration["ScheduleReport:AutoPickLastStartDate"], out bool autopick);

                schedule = new ScheduleReportSettings
                {
                    ScheduleReport = IsSchedule,
                    Frequency = hourlyDelay,
                    StartDate = Configuration["ScheduleReport:StartDate"],
                    ReportForLastDays = reportFor,
                    AutoPickLastStartDate = autopick
                };

            }
            catch (Exception ex)
            {
                schedule = null;
            }

            if (schedule == null)
            {
                log.logMessage += $" Invalid report schedule or data upload settings";
                log.AddLogsToFile(DateTime.UtcNow);
            }
        }

        public SendOutReport SetUpReportSender(AccountConfiguration a)
        {
            if (a?.CustomSMTPSetting != null)
            {
                try
                {
                    smtpServer = new SMTPServer()
                    {
                        EnableSSL = Convert.ToBoolean(a.CustomSMTPSetting.EnableSsl),
                        FromAddress = a.CustomSMTPSetting.SenderEmailAddress,
                        FromName = a.CustomSMTPSetting.SenderName,
                        Login = a.CustomSMTPSetting.Username,
                        Password = a.CustomSMTPSetting.Password,
                        Port = Convert.ToInt32(a.CustomSMTPSetting.Port),
                        Server = a.CustomSMTPSetting.Host,
                    };
                }
                catch
                {
                    smtpServer = null;
                }
            }

            if (smtpServer == null)
            {
                //add log and return
                log.logMessage += $" Invalid smtp details configured!";
                log.AddLogsToFile(DateTime.UtcNow);
                return null;
            }

            return new SendOutReport(smtpServer, log);
        }

        string EmailBodyMaker(bool IsLogs, bool IsNoData, DateTime FromDate, DateTime ToDate, List<string> filenames = null, string PathToEmail = null)
        {
            if (IsLogs)
            {
                if (filenames?.Count() == 0 || filenames == null || PathToEmail == null)
                    return null;

                if (!IsNoData)
                {
                    string emailBody = "<html><body> Hi, <br><br>Detailed logs for your survey dispatches between ";
                    emailBody += "<b>" + FromDate.ToString("dd MMM yyyy") + " to " + ToDate.ToString("dd MMM yyyy") + "</b>" + " are ready and can be downloaded using the links below.<br><br>";

                    emailBody += "In case the logs exceed 100,000 tokens (rows), they will be split across multiple excel files.<br><br>";

                    int sheet = 1;
                    foreach (string file in filenames)
                    {
                        emailBody += "<a href=" + PathToEmail + file + " >" + "Download Detailed Logs " + sheet.ToString() + " </a>";
                        emailBody += " (File " + sheet.ToString() + "/" + filenames.Count().ToString() + ")" + "<br><br>";
                        sheet++;
                    }

                    emailBody += "Note: Download links will expire in 2 days from the day they are generated.<br><br>";

                    emailBody += "Thanks,<br>";
                    emailBody += "Cisco Webex Team<br><br>";
                    emailBody += "We are here to help. Contact us anytime at webexxm-support@cisco.com </body></html>";

                    return emailBody;
                }
                else
                {
                    string emailBody = "<html><body> Hi, <br><br>No invitations were sent in the time period- ";
                    emailBody += "<b>" + FromDate.ToString("dd MMM yyyy") + " to " + ToDate.ToString("dd MMM yyyy") + "</b>" + ".<br><br>";

                    emailBody += "In case the logs exceed 100,000 tokens (rows), they will be split across multiple excel files.<br><br>";

                    int sheet = 1;
                    foreach (string file in filenames)
                    {
                        emailBody += "<a href=" + PathToEmail + file + " >" + "Download Detailed Logs " + sheet.ToString() + " </a>";
                        emailBody += " (File " + sheet.ToString() + "/" + filenames.Count().ToString() + ")" + "<br><br>";
                        sheet++;
                    }

                    emailBody += "Note: Download links will expire in 2 days from the day they are generated.<br><br>";

                    emailBody += "Thanks,<br>";
                    emailBody += "Cisco Webex Team<br><br>";
                    emailBody += "We are here to help. Contact us anytime at webexxm-support@cisco.com </body></html>";

                    return emailBody;
                }
            }
            else
            {
                if (!IsNoData)
                {
                    string emailBody = "<html><body> Hi, <br><br>Operations metrics report for your survey dispatches between ";
                    emailBody += "<b>" + FromDate.ToString("dd MMM yyyy") + " to " + ToDate.ToString("dd MMM yyyy") + "</b>" + " are ready and attached with this email.<br><br>";
                    emailBody += "Thanks,<br>";
                    emailBody += "Cisco Webex Team<br><br>";
                    emailBody += "We are here to help. Contact us anytime at webexxm-support@cisco.com </body></html>";

                    return emailBody;
                }
                else
                {
                    string emailBody = "<html><body>Hi, <br><br>No invitations were sent in the time period- ";
                    emailBody += "<b>" + FromDate.ToString("dd MMM yyyy") + " to " + ToDate.ToString("dd MMM yyyy") + "</b>" + ".<br><br>";
                    emailBody += "Thanks,<br>";
                    emailBody += "Cisco Webex Team<br><br>";
                    emailBody += "We are here to help. Contact us anytime at webexxm-support@cisco.com </body></html>";

                    return emailBody;
                }
            }
        }

        public async Task RunReportTask(DateTime StartDate, int reportFor, double hourlyDelay, SendOutReport sendOutReport, AccountConfiguration a)
        {
            try
            {
                bool IsScheduleReport = schedule.ScheduleReport;

                bool IsValidEmail(string email)
                {
                    try
                    {
                        var mail = new System.Net.Mail.MailAddress(email);
                        return true;
                    }
                    catch
                    { 
                        return false;
                    }
                }

                DateTime NextLockCheck = DateTime.UtcNow;
                var OnDemand = await via.GetOnDemandModel();
                NextLockCheck = NextLockCheck.AddMinutes(2);

                int DeleteEvery = 0;
                string ReportPath = "";
                string PathToEmail = "";

                int DeleteOlderThan = 48; //fixed in  hours

                try
                {
                    int.TryParse(Configuration["DetailedLogs:DeleteEvery"], out DeleteEvery);
                    PathToEmail = Configuration["DetailedLogs:PathToEmail"];
                    ReportPath = Configuration["DetailedLogs:ReportPath"];
                }
                catch(Exception ex)
                {
                    log.logMessage += $" Detailed logs report settings are not set properly. Message- {ex.Message}, Trace- {ex.StackTrace}";
                    log.AddLogsToFile(DateTime.UtcNow);

                    return;
                }

                if (PathToEmail == null || ReportPath == null || DeleteEvery == 0)
                {
                    log.logMessage += $" Detailed logs report settings are not configured. Nulls or 0's are filled in when values were expected.";
                    log.AddLogsToFile(DateTime.UtcNow);

                    return;
                }

                try
                {
                    if (!Directory.Exists(ReportPath))
                    {
                        DirectoryInfo r = System.IO.Directory.CreateDirectory(ReportPath);
                        ReportPath = r.FullName;
                    }
                }
                catch (Exception ex)
                {
                    log.logMessage += $" Cannot delete old detailed logs generated. Unable to create new report path. Stopping service- Message- " + ex.Message + " Trace- " + ex.StackTrace;
                    log.AddLogsToFile(DateTime.UtcNow);
                    return;
                }

                DateTime NextDeleteCheck = DateTime.UtcNow;
                
                while (true)
                {
                    try
                    {
                        if (DeleteEvery > 0 && NextDeleteCheck < DateTime.UtcNow)
                        {
                            DirectoryInfo reportdir = new DirectoryInfo(ReportPath);

                            foreach(FileInfo f in reportdir.GetFiles())
                            {
                                if (Math.Abs(f.CreationTime.Subtract(DateTime.UtcNow).TotalHours) > DeleteOlderThan)
                                {
                                    try
                                    {
                                        File.Delete(Path.Combine(ReportPath, f.Name));
                                    }
                                    catch(Exception ex)
                                    {
                                        log.logMessage += $" Unable to delete file- " + Path.Combine(ReportPath, f.Name) + "- Message- " + ex.Message + " Trace- " + ex.StackTrace;
                                        log.AddLogsToFile(DateTime.UtcNow);
                                        continue;
                                    }
                                }
                            }

                            NextDeleteCheck = DateTime.UtcNow.AddHours(DeleteEvery);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.logMessage += $"Error in deleting old detailed logs report- " + " {ex.Message}    {ex.StackTrace}";
                        log.AddLogsToFile(DateTime.UtcNow);
                        continue;
                    }
                    try
                    {
                        if (IsScheduleReport && StartDate < DateTime.UtcNow)
                        {
                            a = await via.GetAccountConfiguration();

                            string bearer = null;

                            string responseBody = await hTTPWrapper.GetLoginToken(a.WXMAdminUser, a.WXMAPIKey);
                            if (!string.IsNullOrEmpty(responseBody))
                            {
                                BearerToken loginToken = Newtonsoft.Json.JsonConvert.DeserializeObject<BearerToken>(responseBody);
                                bearer = "Bearer " + loginToken.AccessToken;
                            }

                            ReportCreator report = new ReportCreator(Configuration, log, bearer, via);

                            //flow for start date reached or passed
                            FilterBy filter = new FilterBy() { afterdate = DateTime.Today.AddDays(-reportFor), beforedate = DateTime.UtcNow };

                            bool lock_ = await via.LockOnDemand(filter, OnDemand);

                            string Emails = null;
                            if (!a.ExtendedProperties?.Keys?.Contains("ReportRecipients") == true)
                            {
                                log.logMessage += $"No To emails present";
                                log.AddLogsToFile(DateTime.UtcNow);
                                continue;
                            }
                            else if (a.ExtendedProperties?.Keys?.Contains("ReportRecipients") == true && string.IsNullOrEmpty(a.ExtendedProperties["ReportRecipients"]))
                            {
                                log.logMessage += $"No To emails present";
                                log.AddLogsToFile(DateTime.UtcNow);
                                continue;
                            }
                            else
                            {
                                Emails = a.ExtendedProperties["ReportRecipients"];
                            }

                            List<string> toEmails = new List<string>();

                            foreach (string email in Emails.Split(";"))
                            {
                                string e = Regex.Replace(email, @"\s+", "");
                                if (IsValidEmail(e))
                                    toEmails.Add(e);
                            }

                            Tuple<byte[], bool> reportJob = await report.GetOperationMetricsReport(filter);

                            if (reportJob == null)
                            {
                                log.logMessage += "Error in generating the report or no data present to generate report";
                                log.AddLogsToFile(DateTime.UtcNow);
                            }

                            byte[] reportBytes = reportJob?.Item1;

                            StartDate = DateTime.UtcNow.AddHours(hourlyDelay);

                            if (reportBytes?.Count() > 0)
                            {
                                var toAddress = new MailAddress(toEmails.First());

                                MailMessage mailMessage = new MailMessage();
                                mailMessage.From = new MailAddress(smtpServer.FromAddress, smtpServer.FromName);

                                mailMessage.To.Add(toEmails.First());

                                if (toEmails.Count() > 1)
                                {
                                    foreach (var toemail in toEmails.Skip(1))
                                        mailMessage.CC.Add(toemail);
                                }

                                //only metrics report with schedule
                                Stream file = new MemoryStream(reportBytes);
                                mailMessage.Attachments.Add(new Attachment(file, "DPReport-" + filter.afterdate.AddMinutes(OnDemand.TimeOffSet).ToString("yyyyMMdd") + "-" + filter.beforedate.AddMinutes(OnDemand.TimeOffSet).ToString("yyyyMMdd") + ".xlsx"));

                                #region mail content

                                string emailBody = null;

                                mailMessage.IsBodyHtml = true;

                                if (reportJob.Item2)
                                    emailBody = EmailBodyMaker(false, false, filter.afterdate.AddMinutes(OnDemand.TimeOffSet), filter.beforedate.AddMinutes(OnDemand.TimeOffSet));
                                else
                                    emailBody = EmailBodyMaker(false, true, filter.afterdate.AddMinutes(OnDemand.TimeOffSet), filter.beforedate.AddMinutes(OnDemand.TimeOffSet));

                                string emailSubject = "Survey Dispatch Operations Metrics For Cisco Webex Experience Management";

                                #endregion

                                mailMessage.Subject = emailSubject;
                                mailMessage.Body = emailBody;

                                await sendOutReport.SendOutEmails(mailMessage);

                                log.logMessage += "Successfully completed report dispatch on- " + DateTime.UtcNow.ToString() + "\n";
                                log.logMessage += "Next dispatch on- " + StartDate.ToString();
                                log.AddLogsToFile(DateTime.UtcNow);
                            }
                            else
                            {
                                log.logMessage += "Could not dispatch since no data was present";
                                log.AddLogsToFile(DateTime.UtcNow);
                            }

                            //save startdate in a file if app restarts
                            List<Dictionary<string, string>> _data = new List<Dictionary<string, string>>()
                    {
                        new Dictionary<string, string>() {
                            { "StartDate", StartDate.ToString("yyyy-MM-ddTHH:mm:ss")}
                        }
                    };

                            string json = JsonConvert.SerializeObject(_data.ToArray());

                            //write string to file
                            System.IO.File.WriteAllText(Configuration["LogFilePath"] + "/startdate.json", json);

                            bool unlock = await via.UnLockOnDemand();

                            if (unlock == false)
                            {
                                log.logMessage += $"Unable to unlock on demand report";
                            }
                            else
                                OnDemand = await via.GetOnDemandModel();
                        }
                    }
                    catch (Exception ex)
                    {
                        bool unlock = await via.UnLockOnDemand();

                        log.logMessage += $"Error in scheduled report task {ex.Message}    {ex.StackTrace}";
                        continue;
                    }
                    try
                    {
                        if (NextLockCheck < DateTime.UtcNow)
                        {
                            OnDemand = await via.GetOnDemandModel();
                            NextLockCheck = NextLockCheck.AddMinutes(2);
                        }
                        
                        if (OnDemand != null && OnDemand?.IsLocked == true)
                        {
                            a = await via.GetAccountConfiguration();

                            string Emails = null;
                            if (!a.ExtendedProperties?.Keys?.Contains("ReportRecipients") == true)
                            {
                                await via.UnLockOnDemand();

                                log.logMessage += $"No To emails present";
                                log.AddLogsToFile(DateTime.UtcNow);
                                continue;
                            }
                            else if ((a.ExtendedProperties?.Keys?.Contains("ReportRecipients") == true) && string.IsNullOrEmpty(a.ExtendedProperties["ReportRecipients"]))
                            {
                                await via.UnLockOnDemand();

                                log.logMessage += $"No To emails present";
                                log.AddLogsToFile(DateTime.UtcNow);
                                continue;
                            }
                            else
                            {
                                Emails = a.ExtendedProperties["ReportRecipients"];
                            }

                            List<string> toEmails = new List<string>();

                            foreach (string email in Emails.Split(";"))
                            {
                                string e = Regex.Replace(email, @"\s+", "");
                                if (IsValidEmail(e))
                                    toEmails.Add(e);
                            }

                            string bearer = null;

                            string responseBody = await hTTPWrapper.GetLoginToken(a.WXMAdminUser, a.WXMAPIKey);
                            if (!string.IsNullOrEmpty(responseBody))
                            {
                                BearerToken loginToken = Newtonsoft.Json.JsonConvert.DeserializeObject<BearerToken>(responseBody);
                                bearer = "Bearer " + loginToken.AccessToken;
                            }

                            ReportCreator report = new ReportCreator(Configuration, log, bearer, via);

                            List<string> filenames = new List<string>();

                            Tuple<byte[], bool> reportJob = null;

                            if (!OnDemand.OnlyLogs)
                            {
                                reportJob = await report.GetOperationMetricsReport(OnDemand.Filter, OnDemand.OnlyLogs);

                                if (reportJob == null)
                                {
                                    log.logMessage += "Error in generating the report or no data present to generate report";
                                    log.AddLogsToFile(DateTime.UtcNow);
                                }
                            }
                            else
                            {
                                long total = await via.GetMergedDataCount(OnDemand.Filter);

                                if (total > 0)
                                {
                                    for(int i = 0; i <= total; i = i + 100000)
                                    {
                                        Tuple<byte[], bool> detailedlogs = await report.GetOperationMetricsReport(OnDemand.Filter, OnDemand.OnlyLogs, i, 100000);

                                        if (detailedlogs.Item1?.Count() > 0)
                                        {
                                            ReportFileManagement store = new ReportFileManagement(ReportPath, log);
                                            string filename = store.SaveReportFile(detailedlogs.Item1, OnDemand.Filter.afterdate.AddMinutes(OnDemand.TimeOffSet), OnDemand.Filter.beforedate.AddMinutes(OnDemand.TimeOffSet), OnDemand.TimeOffSet);

                                            if (filename == null || PathToEmail == null)
                                            {
                                                log.logMessage += $"unable to store report file or PathToEmail not set";
                                                log.AddLogsToFile(DateTime.UtcNow);

                                                await via.UnLockOnDemand();

                                                OnDemand = await via.GetOnDemandModel();

                                                continue;
                                            }

                                            filenames.Add(filename);
                                        }
                                        else
                                        {
                                            log.logMessage += $"unable to generate detailed logs report";
                                            log.AddLogsToFile(DateTime.UtcNow);

                                            await via.UnLockOnDemand();

                                            OnDemand = await via.GetOnDemandModel();

                                            continue;
                                        }
                                    }
                                }
                            }

                            byte[] reportBytes = reportJob?.Item1;

                            if ((reportBytes?.Count() > 0 && !OnDemand.OnlyLogs) || (filenames.Count() > 0 && OnDemand.OnlyLogs))
                            {
                                MailMessage mailMessage = new MailMessage();
                                mailMessage.From = new MailAddress(smtpServer.FromAddress, smtpServer.FromName);

                                mailMessage.To.Add(toEmails.First());

                                if (toEmails.Count() > 1)
                                {
                                    foreach (var toemail in toEmails.Skip(1))
                                        mailMessage.CC.Add(toemail);
                                }

                                if (!OnDemand.OnlyLogs)
                                {
                                    Stream file = new MemoryStream(reportBytes);
                                    mailMessage.Attachments.Add(new Attachment(file, "DPReport-" + OnDemand.Filter.afterdate.AddMinutes(OnDemand.TimeOffSet).ToString("yyyyMMdd") + "-" + OnDemand.Filter.beforedate.AddMinutes(OnDemand.TimeOffSet).ToString("yyyyMMdd") + ".xlsx"));
                                }
                                #region email content

                                string emailBody = null;

                                mailMessage.IsBodyHtml = true;

                                if (reportJob?.Item2 == true || filenames?.Count() > 0)
                                {
                                    if (OnDemand.OnlyLogs)
                                        emailBody = EmailBodyMaker(true, false, OnDemand.Filter.afterdate.AddMinutes(OnDemand.TimeOffSet), OnDemand.Filter.beforedate.AddMinutes(OnDemand.TimeOffSet), filenames, PathToEmail);
                                    else
                                        emailBody = EmailBodyMaker(false, false, OnDemand.Filter.afterdate.AddMinutes(OnDemand.TimeOffSet), OnDemand.Filter.beforedate.AddMinutes(OnDemand.TimeOffSet));
                                }
                                else
                                {
                                    if (OnDemand.OnlyLogs)
                                        emailBody = EmailBodyMaker(true, true, OnDemand.Filter.afterdate.AddMinutes(OnDemand.TimeOffSet), OnDemand.Filter.beforedate.AddMinutes(OnDemand.TimeOffSet), filenames, PathToEmail);
                                    else
                                        emailBody = EmailBodyMaker(false, true, OnDemand.Filter.afterdate.AddMinutes(OnDemand.TimeOffSet), OnDemand.Filter.beforedate.AddMinutes(OnDemand.TimeOffSet));
                                }
                                

                                string emailSubject = "";

                                if (!OnDemand.OnlyLogs)
                                    emailSubject = "Survey Dispatch Operations Metrics For Cisco Webex Experience Management";
                                else
                                    emailSubject = "Survey Dispatch Detailed Logs For Cisco Webex Experience Management";

                                #endregion

                                mailMessage.Subject = emailSubject;
                                mailMessage.Body = emailBody;
                                
                                await sendOutReport.SendOutEmails(mailMessage);
                                
                                log.logMessage += "Successfully completed on demand report dispatch on- " + DateTime.UtcNow.ToString() + "\n";
                                log.AddLogsToFile(DateTime.UtcNow);
                            }

                            bool unlock = await via.UnLockOnDemand();

                            if (unlock == false)
                            {
                                log.logMessage += $"Unable to unlock on demand report";
                                OnDemand = await via.GetOnDemandModel();
                            }
                            else
                                OnDemand = await via.GetOnDemandModel();
                        }
                    }
                    catch (Exception ex)
                    {
                        bool unlock = await via.UnLockOnDemand();

                        OnDemand = await via.GetOnDemandModel();

                        log.logMessage += $"Error in on demand report task {ex.Message}    {ex.StackTrace}";
                        log.AddLogsToFile(DateTime.UtcNow);
                        
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                log.logMessage += $"Error in report task {ex.Message}    {ex.StackTrace}";
                log.AddLogsToFile(DateTime.UtcNow);
                return;
            }

        }

    }
}
