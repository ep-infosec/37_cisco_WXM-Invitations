using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InvitationNotification
{
    class LogFileManagement
    {
        string path = null;
        DateTime utcNow;
        ApplicationLog log;
        public LogFileManagement(string configuredPath, ApplicationLog appLog)
        {
            path = configuredPath;
            log = appLog;
        }
        public string SaveLogFile(List<ProjectedLog> logs, bool isEod = false, bool IsAccountlevel = false)
        {
            try
            {
                string logMessages = "Time,Dispatch Id,Batch Id,Log Type, Message\n";
                string level = logs.First().LogLevel;
                foreach (var log in logs)
                {
                    logMessages += $"{log.Created.ToString()},{log.DispatchId},{log.BatchId},{log.LogLevel},{log.Message}\n";
                }
                string fileNamePrefix = IsAccountlevel ? isEod ? "AccountLevelEODLog" : "AccountLevelRealTimeLog" : isEod ? "EODLog" : "RealTimeLog";

                var fileName = $"{fileNamePrefix}{level}{DateTime.UtcNow.ToString("yyMMddHHmmssfff")}.log";

                string fullFilepath = Path.Combine(path, fileName);

                using (StreamWriter sw = File.CreateText(fullFilepath))
                {
                    sw.WriteLine(logMessages);
                    sw.Close();
                    logMessages = null;
                }

                return fileName;
            }
            catch (Exception ex0)
            {
                log.logMessage += $"{ex0.Message}   {ex0.StackTrace}\n";
                return null;
            }

        }
        public bool DeleteFile(string fileName)
        {
            //TODO
            return true;
        }
    }
}
