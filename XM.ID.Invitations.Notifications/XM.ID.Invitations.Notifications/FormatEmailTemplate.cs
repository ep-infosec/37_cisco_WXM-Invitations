using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using XM.ID.Net;

namespace InvitationNotification
{
    class FormatEmailTemplate
    {
        SMTPServer smtp;
        string fileBaseUrl;
        ApplicationLog log;
        public FormatEmailTemplate(SMTPServer smtpserver, string url, ApplicationLog appLog)
        {
            smtp = smtpserver;
            fileBaseUrl = url;
            log = appLog;
        }

        public MailMessage RealTimeTemplateByDispatch(DispatchChannel dispatch, List<ProjectedLog> logs, string level, int count, string fileName)
        {

            List<MailMessage> mailMessages = new List<MailMessage>();
            var logsByLevel = logs.GroupBy(x => x.LogLevel).ToDictionary(x => x.Key, y => y.ToList());
            string subject = "Invitations Notification: ";

            string emailIds = null;
            if (level == "Failure")
            {
                emailIds = dispatch.Notify.F;
                if (!string.IsNullOrEmpty(emailIds))
                    return FormMailMessage(emailIds, GetrealTimetemplateForfailure(logs, dispatch.DispatchName), subject + "Failure", count, fileName);

            }
            else if (level == "Error")
            {
                emailIds = dispatch.Notify.E;
                if (!string.IsNullOrEmpty(emailIds))
                    return FormMailMessage(emailIds, GetRealTimeTemplateForError(logs, dispatch.DispatchName), subject + "Error", count, fileName);
            }

            return null;

        }


        public MailMessage EndOfDayTemplatesByDispatch(DispatchChannel dispatch, List<ProjectedLog> logs, string level, int count, string fileName)
        {
            try
            {

                string subject = "Daily Round For Invitations: ";

                string emailIds = null;
                if (level == "failure")
                {
                    emailIds = dispatch.Notify.F;
                    if (!string.IsNullOrEmpty(emailIds))
                        return FormMailMessage(emailIds, GetEODTemplateForFailure(logs, dispatch.DispatchName), subject + "Failure", count, fileName);

                }
                else if (level == "Error")
                {
                    emailIds = dispatch.Notify.E;
                    if (!string.IsNullOrEmpty(emailIds))
                        return FormMailMessage(emailIds, GetEODTemplateForError(logs, dispatch.DispatchName), subject + "Error", count, fileName);
                }
                else if (level == "Information")
                {
                    emailIds = dispatch.Notify.I;
                    if (!string.IsNullOrEmpty(emailIds))
                        return FormMailMessage(emailIds, GetEODTemplateForInfo(logs, dispatch.DispatchName), subject + "Information", count, fileName);
                }
                else if (level == "Warning")
                {
                    emailIds = dispatch.Notify.W;
                    if (!string.IsNullOrEmpty(emailIds))
                        return FormMailMessage(emailIds, GetEODTemplateForWarning(logs, dispatch.DispatchName), subject + "Warning", count, fileName);
                }
                else if (level == "Debug")
                {
                    emailIds = dispatch.Notify.D;
                    if (!string.IsNullOrEmpty(emailIds))
                    {
                        var debugLogs = GetEODTemplateForDebug(logs, dispatch.DispatchName);
                        return FormMailMessage(emailIds, debugLogs, subject + "Debug", count, fileName);
                    }
                }

                return null;
            }
            catch (Exception ex0)
            {
                log.logMessage += $"{ex0.Message}   {ex0.StackTrace}\n";
                return null;
            }

        }

        #region Account Level Logs
        public MailMessage AccountLevelTemplate(List<ProjectedLog> logs, string emailIds, int count, string fileName, bool isEod = false)
        {
            try
            {
                string subject = isEod ? $"Daily Round For Invitations  " : $"Invitations Notification:";

                var body = GetRealTimeTemplateForAccountLevel(logs);

                if (!string.IsNullOrEmpty(emailIds))
                    return FormMailMessage(emailIds, body, subject, count, fileName);

                return null;
            }
            catch (Exception ex0)
            {
                log.logMessage += $"{ex0.Message}   {ex0.StackTrace}\n";
                return null;
            }

        }
        public string GetRealTimeTemplateForAccountLevel(List<ProjectedLog> logsByBatch)
        {
            var innerMessageBody = GetInnerBodyMessageForAccountlevel(logsByBatch);

            if (string.IsNullOrEmpty(innerMessageBody))
                return null;

            var message = $"Hi, \n" +
                $" Please find the details below.\n\n" +
                $" {innerMessageBody} ";

            return message;
        }

        #endregion

        #region RealTime
        public string GetrealTimetemplateForfailure(List<ProjectedLog> logsByBatch, string dispatchname)
        {
            var innerMessage = "Please find the details for the failure below. \n\n";
            var innerBody = GetInnerBodyMessage(logsByBatch);
            if (!string.IsNullOrEmpty(innerBody))
                innerMessage += innerBody;

            var realtimefailure = $"Hi, \n" +
                $" We encountered an error while sending invites.\n\n" +
                $" Dispatch: {dispatchname} \n\n" +
                $" {innerMessage}";
            return realtimefailure;
        }


        private string GetRealTimeTemplateForError(List<ProjectedLog> logsByBatch, string dispatchname)
        {
            var innerMessage = "Please find the details for the error below. \n\n";
            var innerBody = GetInnerBodyMessage(logsByBatch);
            if (!string.IsNullOrEmpty(innerBody))
                innerMessage += innerBody;

            var realtimefailure = $"Hi, \n" +
                $" We encountered an error while sending invites.\n\n" +
                $" Dispatch: {dispatchname} \n\n" +
                $" {innerMessage}";
            return realtimefailure;
        }

        public string GetRealTimeTemplateForInfo(List<ProjectedLog> logsByBatch, string dispatchname)
        {
            var innerMessage = "Please find the details below. \n\n";
            var innerBody = GetInnerBodyMessage(logsByBatch);
            if (!string.IsNullOrEmpty(innerBody))
                innerMessage += innerBody;

            var realtimefailure = $"Hi, \n" +
                $" Please check the following info about invites being sent.\n\n" +
                $" Dispatch: {dispatchname} \n\n" +
                $" {innerMessage} ";
            return realtimefailure;
        }

        #endregion

        #region EOD
        public string GetEODTemplateForDebug(List<ProjectedLog> logsByBatch, string dispatchName)
        {
            var innerMessage = "Please find the details in the attached file. \n\n";
            var innerBody = GetInnerBodyMessage(logsByBatch);
            if (!string.IsNullOrEmpty(innerBody))
                innerMessage += innerBody;

            var realtimefailure = $"Hi, \n" +
                $"Below are debug notes for the various dispatches today {DateTime.UtcNow.AddDays(-1).ToString("dd-MMM-yy")} \n\n " +
                $"Dispatch: {dispatchName}\n\n " +
                 $"{innerMessage}";


            return realtimefailure;
        }
        public string GetEODTemplateForWarning(List<ProjectedLog> logsByBatch, string dispatchName)
        {
            var innerMessage = "Please find the details below. \n\n";
            var innerBody = GetInnerBodyMessage(logsByBatch);
            if (!string.IsNullOrEmpty(innerBody))
                innerMessage += innerBody;

            var realtimefailure = $"Hi, \n" +
                $"Please check the following warnings about invites being sent today {DateTime.UtcNow.AddDays(-1).ToString("dd-MMM-yy")} \n\n " +
                $"Dispatch: {dispatchName}\n\n " +
                $"{innerMessage}";


            return realtimefailure;
        }
        public string GetEODTemplateForInfo(List<ProjectedLog> logsByBatch, string dispatchName)
        {
            var innerMessage = "Please find the details below. \n\n";
            var innerBody = GetInnerBodyMessage(logsByBatch);
            if (!string.IsNullOrEmpty(innerBody))
                innerMessage += innerBody;

            var realtimefailure = $"Hi, \n" +
                $" Please check the following info about invites being sent today {DateTime.UtcNow.AddDays(-1).ToString("dd-MMM-yy")} \n\n " +
                $"Dispatch: {dispatchName}\n\n " +
                $"{innerMessage} ";


            return realtimefailure;
        }
        public string GetEODTemplateForError(List<ProjectedLog> logsByBatch, string dispatchName)
        {
            var innerMessage = "Please find the details below. \n\n";
            var innerBody = GetInnerBodyMessage(logsByBatch);
            if (!string.IsNullOrEmpty(innerBody))
                innerMessage += innerBody;

            var realtimefailure = $"Hi, \n" +
                $"Below are errors faced for the various dispatches today {DateTime.UtcNow.AddDays(-1).ToString("dd-MMM-yy")} \n\n " +
                $"Dispatch: {dispatchName}\n\n " +
                $"{innerMessage}";


            return realtimefailure;
        }


        public string GetEODTemplateForFailure(List<ProjectedLog> logsByBatch, string dispatchName)
        {
            var innerMessage = "Please find the details below f. \n\n";
            var innerBody = GetInnerBodyMessage(logsByBatch);
            if (!string.IsNullOrEmpty(innerBody))
                innerMessage += innerBody;

            var realtimefailure = $"Hi, \n" +
                $"Below are failures the dispatch today {DateTime.UtcNow.AddDays(-1).ToString("dd-MMM-yy")} \n\n " +
                $"Dispatch: {dispatchName}\n\n " +
                $"{innerMessage} ";


            return realtimefailure;
        }

        #endregion


        private string GetInnerBodyMessage(List<ProjectedLog> logs)
        {
            var allNonBatchid = logs.FindAll(x => string.IsNullOrEmpty(x.BatchId));
            logs.RemoveAll(x => string.IsNullOrEmpty(x.BatchId));

            var logsbyBatch = logs.GroupBy(x => x.BatchId).ToDictionary(x => x.Key, y => y.ToList());

            string logdetails = null;
            if (logsbyBatch == null)
                return null;

            foreach (var batchLog in logsbyBatch)
            {
                if (!string.IsNullOrEmpty(batchLog.Key))
                {
                    logdetails += $"\n\nBatch: {batchLog.Key} \n\n";
                    foreach (var log in batchLog.Value)
                    {
                        logdetails += $"{log.Created.ToString()}    {log.Message} \n";
                    }
                }

            }
            if (allNonBatchid?.Count() > 0)
            {
                logdetails += $"\n\nBatch: \n\n";
                foreach (var log in allNonBatchid)
                {
                    logdetails += $"{log.Created.ToString()}    {log.Message} \n";
                }
            }
            return logdetails;

        }
        private string GetInnerBodyMessageForAccountlevel(List<ProjectedLog> logs)
        {

            var orderedLog = logs?.OrderByDescending(x => x.Created)?.ToList() ?? null;

            string logdetails = null;
            if (orderedLog == null)
                return null;

            foreach (var log in orderedLog)
            {
                string batchId = null;
                string dispachId = null;
                if (!string.IsNullOrEmpty(log.BatchId))
                    batchId = $"BatchId : {log.BatchId}";

                logdetails += $"{log.Created.ToString()}   {batchId} {log.Message} \n";

            }
            return logdetails;

        }
        MailMessage FormMailMessage(string toids, string emailBody, string subject, int count, string fileName, string attachementbody = null)
        {
            try
            {
                var fromAddress = new MailAddress(smtp.FromAddress, smtp.FromName);
                var toemails = toids.Split(';').ToList();
                var toAddress = new MailAddress(toemails.First());

                var mailmessage = new MailMessage(fromAddress, toAddress);
                mailmessage.Subject = subject;


                if (count > 0 && !string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(fileBaseUrl))
                {
                    var paths = new string[] { fileBaseUrl, fileName };
                    var fullPath = Path.Combine(paths);
                    emailBody += $"\n\n {count} more log entries available at {fullPath} \n You will need to sign in to download the log file. Please reach out to your account admin for credentials";
                }
                emailBody += $"\n\n Thanks.";

                mailmessage.Body = emailBody;
                if (toemails.Count() > 1)
                {
                    foreach (var toemail in toemails.Skip(1))
                        mailmessage.CC.Add(toemail);
                }
                if (!string.IsNullOrEmpty(attachementbody))
                {
                    byte[] byteattachement = Encoding.ASCII.GetBytes(attachementbody);
                    Stream file = new MemoryStream(byteattachement);
                    mailmessage.Attachments.Add(new Attachment(file, "Logs.txt"));
                }

                return mailmessage;
            }
            catch (Exception ex0)
            {
                log.logMessage += $"{ex0.Message}   {ex0.StackTrace}\n";
                return null;
            }

        }

    }
}
