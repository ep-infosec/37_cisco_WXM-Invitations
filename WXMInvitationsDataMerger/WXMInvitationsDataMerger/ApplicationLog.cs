using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WXMInvitationsDataMerger
{
    public class ApplicationLog
    {
        public string logMessage;
        string applicationLogPath;
        public ApplicationLog(string logpath)
        {
            applicationLogPath = logpath;
        }
        public void AddLogsToFile(DateTime utcNow, string message = null)
        {
            string msg = logMessage;
            try
            {
                if (!string.IsNullOrEmpty(message))
                    msg += message;

                if (string.IsNullOrEmpty(msg))
                    return;


                var fileName = $"{utcNow.ToString("yyMMddHH")}.log";
                string path = Path.Combine(applicationLogPath, fileName);

                using (StreamWriter sw = (File.Exists(path)) ? File.AppendText(path) : File.CreateText(path))
                {
                    sw.WriteLine(DateTime.UtcNow.ToString());
                    sw.WriteLine(msg);
                    sw.Close();
                    logMessage = null;
                }
            }
            catch (Exception ex0)
            {
                Console.WriteLine(ex0);
            }

        }

    }
}
