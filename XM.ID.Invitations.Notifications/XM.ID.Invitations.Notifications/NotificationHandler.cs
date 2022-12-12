using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using XM.ID.Net;

namespace InvitationNotification
{
    class NotificationHandler
    {

        #region vars
        IConfigurationRoot configuration;
        ViaDataBase viaDb;
        SemaphoreSlim notificationRunnerLock = new SemaphoreSlim(1, 1);
        SMTPServer smtpServer;
        CancellationToken cancellationToken;
        ApplicationLog log;
        string emailPathForLog;
        string applicationLog;
        string logFilePath;
        bool accountLevelIsEOD;
        LogFileManagement logFileManagement;
        DateTime lastEdoSent;
        #endregion
        public NotificationHandler(IConfigurationRoot config, ApplicationLog appLog, CancellationToken token)
        {
            configuration = config;
            var databaseConnectionstring = configuration["DBConnectionString"];
            var dbname = configuration["DBName"];
            cancellationToken = token;
            log = appLog;

            IMongoDatabase mongoDb = GetMongoDB(databaseConnectionstring, dbname);
            viaDb = new ViaDataBase(mongoDb, log);
            log = appLog;

        }

        public async Task NotificationWorker()
        {
            try
            {
                #region Read from Config
                var frequency = configuration["Frequency:Every"];

                int delay = 5;
                int realtImeMaxLevel = 2;
                if (frequency != null)
                {
                    if (frequency?.ToLower() == "hour")
                    {
                        int.TryParse(configuration["Frequency:Hour"], out int hour);
                        delay = hour * 60;
                    }
                    else if (frequency?.ToLower() == "minute")
                    {
                        int.TryParse(configuration["Frequency:Minute"], out int minute);
                        delay = minute;
                    }
                    int.TryParse(configuration["Frequency:RealtImeMaxLevel"], out realtImeMaxLevel);

                    bool.TryParse(configuration["Frequency:AccountLevleIsEod"], out accountLevelIsEOD);
                }
                if (delay < 1)
                    delay = 5;
                log.logMessage += $"Frequency : {delay}\n";

                //get details from account configuration 
                var accountConfig = await viaDb.GetAccountdetails();
                #region Check CustomSMTP

                bool.TryParse(accountConfig.CustomSMTPSetting.EnableSsl, out bool enablessl);
                int.TryParse(accountConfig.CustomSMTPSetting.Port, out int port);
                smtpServer = new SMTPServer()
                {
                    EnableSSL = enablessl,
                    FromAddress = accountConfig.CustomSMTPSetting.SenderEmailAddress,
                    FromName = accountConfig.CustomSMTPSetting.SenderName,
                    Login = accountConfig.CustomSMTPSetting.Username,
                    Password = accountConfig.CustomSMTPSetting.Password,
                    Port = port,
                    Server = accountConfig.CustomSMTPSetting.Host,

                };
                #endregion
                if (smtpServer == null)
                {
                    //add log and return
                    log.logMessage += $"Invalid smtp details configured!\n";
                    return;
                }
                emailPathForLog = configuration["PathToEmail"];
                applicationLog = configuration["ApplicationLogpath"];
                logFilePath = configuration["LogFilePath"];

                logFileManagement = new LogFileManagement(logFilePath, log);
                #endregion

                await StartNotificationTask(delay, realtImeMaxLevel);
            }
            catch (Exception ex0)
            {
                log.logMessage += $"{ex0.Message}    {ex0.StackTrace}\n";

                return;
            }


        }


        public async Task SendNotifications(int minutes, DateTime utcNow, int realtImeMaxLevel = 2)
        {
            try
            {

                //get details from account configuration 
                var accountConfig = await viaDb.GetAccountdetails();
                if (accountConfig == null)
                {
                    log.logMessage += "Invalid or no Account details configured at partner hosted side\n";
                    return;
                }

                string accountLevelEmailIds = null;
                if (accountConfig.ExtendedProperties != null && accountConfig.ExtendedProperties.ContainsKey("AccountNotifications"))
                {
                    accountLevelEmailIds = accountConfig.ExtendedProperties["AccountNotifications"];
                }



                Dictionary<string, int> MaxLevelByDispatch = new Dictionary<string, int>();
                List<DispatchChannel> dispatchWithNotify = new List<DispatchChannel>();
                List<string> levels = new List<string>();
                //get all the dispatch that has the notification configured, and whats the maximim level attached to it
                foreach (var dispatchSettings in accountConfig.DispatchChannels)
                {
                    int maxLogLevel = 5;
                    if (dispatchSettings.Notify != null)
                    {
                        if (string.IsNullOrEmpty(dispatchSettings.Notify.D))
                        {
                            maxLogLevel = 4;
                            if (string.IsNullOrEmpty(dispatchSettings.Notify.W))
                            {
                                maxLogLevel = 3;
                                if (string.IsNullOrEmpty(dispatchSettings.Notify.I))
                                {
                                    maxLogLevel = 2;
                                    if (string.IsNullOrEmpty(dispatchSettings.Notify.E))
                                    {
                                        maxLogLevel = 1;
                                        if (string.IsNullOrEmpty(dispatchSettings.Notify.F))
                                            maxLogLevel = 0;
                                    }
                                }
                            }
                        }
                    }

                    if (maxLogLevel > 0)
                    {
                        dispatchWithNotify.Add(dispatchSettings);
                        MaxLevelByDispatch.Add(dispatchSettings.DispatchId, maxLogLevel);
                    }

                }
                if (string.IsNullOrEmpty(accountLevelEmailIds) && (dispatchWithNotify == null || dispatchWithNotify.Count < 1))
                {
                    log.logMessage += "Notifications not configured for any dispatch\n";
                    return;
                }

                #region EOD
                bool isEOD = false;
                //Make 
                if (utcNow.Hour == 0 && lastEdoSent.Day != utcNow.Day)
                {
                    isEOD = true;
                    lastEdoSent = DateTime.UtcNow;
                    log.logMessage += $"Is Eod\n";
                }
                #endregion


                //get logs from Mongo Db
                #region Real Time
                var realTimeLogs = await viaDb.GetLogsToNotify(minutes, MaxLevelByDispatch, realtImeMaxLevel, false);

                #endregion

                #region EOD
                Dictionary<string, List<ProjectedLog>> eodLogs = new Dictionary<string, List<ProjectedLog>>();
                if (isEOD && realtImeMaxLevel < 5) //if all DWIEF is set to realtime, then no EOD to go out
                {
                    eodLogs = await viaDb.GetLogsToNotify(minutes, MaxLevelByDispatch, realtImeMaxLevel, true);
                    log.logMessage += $"EOD notification should be sent ThreadID {Thread.CurrentThread.ManagedThreadId}\n";
                }
                #endregion

                #region AccountLevel 
                Dictionary<string, List<ProjectedLog>> accountLevelLogs = new Dictionary<string, List<ProjectedLog>>();
                if (!string.IsNullOrEmpty(accountLevelEmailIds))
                {
                    if (accountLevelIsEOD) // based on the setting to have the Account level log either same as EOD of similar to near-real time configuration
                    {
                        if (isEOD)
                            accountLevelLogs = await viaDb.GetLogsToNotify(minutes, MaxLevelByDispatch, realtImeMaxLevel, true, true);
                    }
                    else
                        accountLevelLogs = await viaDb.GetLogsToNotify(minutes, MaxLevelByDispatch, realtImeMaxLevel, false, true);

                }
                #endregion


                #region format template 

                var formatmailtemplate = new FormatEmailTemplate(smtpServer, emailPathForLog, log);
                List<MailMessage> mailMessages = new List<MailMessage>();

                #region Account Level
                if (accountLevelLogs != null && accountLevelLogs.Count > 0)
                {
                    var allLogs = accountLevelLogs.First().Value;
                    if (accountLevelIsEOD) //EOD
                    {
                        var saveFile = GetSavedLogFile(allLogs, true, true);

                        var messages = formatmailtemplate.AccountLevelTemplate(saveFile.Item1, accountLevelEmailIds, saveFile.Item3, saveFile.Item2, true);
                        if (messages != null)
                            mailMessages.Add(messages);
                    }
                    else //near real time by frequency
                    {
                        var saveFile = GetSavedLogFile(allLogs, false, true);
                        var messages = formatmailtemplate.AccountLevelTemplate(saveFile.Item1, accountLevelEmailIds, saveFile.Item3, saveFile.Item2);
                        if (messages != null)
                            mailMessages.Add(messages);
                    }
                }
                #endregion
                #region By Dispatch
                foreach (var dispatch in dispatchWithNotify)
                {
                    #region realTime 
                    if (realTimeLogs != null && realTimeLogs.ContainsKey(dispatch.DispatchId))
                    {
                        var allLogs = realTimeLogs[dispatch.DispatchId];
                        var logsByLevel = allLogs.GroupBy(x => x.LogLevel).ToDictionary(x => x.Key, y => y.ToList());
                        foreach (var logs in logsByLevel)
                        {
                            var saveFile = GetSavedLogFile(logs.Value);
                            var messages = formatmailtemplate.RealTimeTemplateByDispatch(dispatch, saveFile.Item1, logs.Key, saveFile.Item3, saveFile.Item2);
                            if (messages != null)
                                mailMessages.Add(messages);
                        }
                    }
                    else
                    {
                        log.logMessage += "No realtime Logs Found\n";
                    }
                    #endregion
                    #region EOD
                    if (isEOD)
                    {
                        if (eodLogs != null && eodLogs.ContainsKey(dispatch.DispatchId))
                        {
                            var allLogs = eodLogs[dispatch.DispatchId];
                            var logsByLevel = allLogs.GroupBy(x => x.LogLevel).ToDictionary(x => x.Key, y => y.ToList());
                            foreach (var log in logsByLevel)
                            {
                                var saveFile = GetSavedLogFile(log.Value, true);

                                var messages = formatmailtemplate.EndOfDayTemplatesByDispatch(dispatch, saveFile.Item1, log.Key, saveFile.Item3, saveFile.Item2);
                                if (messages != null)
                                    mailMessages.Add(messages);
                            }


                        }
                        else
                            log.logMessage += "No EOD log found\n";
                    }
                    #endregion
                }
                #endregion
                #endregion

                #region Send Out Invitation 
                var sendOutNotification = new SendOutNotification(smtpServer);
                await sendOutNotification.SendOutEmails(mailMessages);

                #endregion
                //mark documnet notified 
                await viaDb.MarkNotificationSent(minutes, MaxLevelByDispatch, realtImeMaxLevel);
            }
            catch (Exception ex0)
            {
                log.logMessage += $"{ex0.Message}   {ex0.StackTrace}";
            }
        }

        (List<ProjectedLog>, string, int) GetSavedLogFile(List<ProjectedLog> allLogs, bool isEod = false, bool isAccountLevel = false)
        {
            var displayLog = allLogs?.Count > 0 ? allLogs.Take(10).ToList() : null;
            if (allLogs.Count > 10)
            {
                var savelog = allLogs.Skip(10).ToList();
                int count = savelog.Count();
                //save the file 
                string savedFileName = logFileManagement.SaveLogFile(savelog, isEod, isAccountLevel);
                return (displayLog, savedFileName, count);
            }
            return (displayLog, null, 0);
        }

        #region Utils
        private IMongoDatabase GetMongoDB(string connectionString, string dbName)
        {
            MongoClientSettings settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
            settings.MaxConnectionIdleTime = TimeSpan.FromMinutes(3);
            settings.ConnectTimeout = TimeSpan.FromSeconds(20);
            settings.MaxConnectionPoolSize = 1000;
            settings.ReadPreference = ReadPreference.Nearest;
            var mongoClient = new MongoClient(settings);
            return mongoClient.GetDatabase(dbName);
        }
        public async Task StartNotificationTask(int delayInMinute, int realtImeMaxLevel = 2)
        {

            if (notificationRunnerLock.CurrentCount == 0)
                return; // Already Started

            await Task.Factory.StartNew(async (x) =>
           {
               if (await notificationRunnerLock.WaitAsync(100))
               {
                   try
                   {
                       while (true)
                       {
                           var utcNow = DateTime.UtcNow;
                           log.logMessage += $"Send Notification start ThreadID {Thread.CurrentThread.ManagedThreadId}";
                           await SendNotifications(delayInMinute, utcNow, realtImeMaxLevel);
                           log.AddLogsToFile(utcNow);
                           await Task.Delay(delayInMinute * 60 * 1000);
                       }

                   }
                   finally
                   {
                       notificationRunnerLock.Release();
                   }
               }
           }, cancellationToken, TaskCreationOptions.LongRunning);
            return;
        }

        #endregion

    }
}
