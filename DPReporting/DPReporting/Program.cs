using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using XM.ID.Invitations.Net;

namespace DPReporting
{
    class Program
    {
        private static ApplicationLog log;
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                                    .AddJsonFile("appsettings.json", false, true)
                                    .Build();

            string appLogpath = configuration["LogFilePath"];

            try
            {
                if (!Directory.Exists(appLogpath))
                {
                    DirectoryInfo logpath = System.IO.Directory.CreateDirectory(appLogpath);
                    appLogpath = logpath.FullName;
                }
            }
            catch (Exception ex)
            {
                appLogpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DPReportLogs");
            }

            log = new ApplicationLog(appLogpath);

            log.logMessage = $" {DateTime.UtcNow.ToString()}    Process started";

            ViaMongoDB via = new ViaMongoDB(configuration);

            ReportTask k = new ReportTask(configuration, log, via);

            Console.WriteLine("keep an eye on log files generated in case process fails or to keep track of reporting");

            if (SharedSettings.BASE_URL == null)
                SharedSettings.BASE_URL = configuration["WXM_BASE_URL"];

            await k.ReportSendingTask();
        }
    }
}
