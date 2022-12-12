using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using XM.ID.Invitations.Net;
using XM.ID.Net;

namespace WXMInvitationsDataMerger
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
                appLogpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataMergingLogs");
            }

            log = new ApplicationLog(appLogpath);

            log.logMessage = $" {DateTime.UtcNow.ToString()}    Process started";

            ViaMongoDB via = new ViaMongoDB(configuration);

            DataUpload d = new DataUpload(configuration, log, via);

            Console.WriteLine("keep an eye on log files generated");

            if (SharedSettings.BASE_URL == null)
                SharedSettings.BASE_URL = configuration["WXM_BASE_URL"];

            DataUploadSettings dataupload = null;

            try
            {
                double.TryParse(configuration["DataUploadSettings:RunUploadEveryMins"], out double uploadEvery);
                double.TryParse(configuration["DataUploadSettings:UploadDataForLastHours"], out double uploadFor);
                double.TryParse(configuration["DataUploadSettings:CheckResponsesCapturedForLastHours"], out double ResponsesCheck);

                dataupload = new DataUploadSettings
                {
                    RunUploadEveryMins = uploadEvery,
                    UploadDataForLastHours = uploadFor,
                    CheckResponsesCapturedForLastHours = ResponsesCheck
                };
            }
            catch (Exception ex)
            {
                log.logMessage += $" Invalid data upload settings. Error- " + ex.Message + " trace- " + ex.StackTrace;
                log.AddLogsToFile(DateTime.UtcNow);
            }

            DateTime NextUpload = DateTime.UtcNow;

            DateTime NextLockCheck = DateTime.UtcNow;
            var OnDemand = await via.GetOnDemandModel();
            NextLockCheck = NextLockCheck.AddMinutes(2);

            int SortOrder = -1;

            while (true)
            {
                try
                {
                    if (NextLockCheck < DateTime.UtcNow)
                    {
                        OnDemand = await via.GetOnDemandModel();
                        NextLockCheck = NextLockCheck.AddMinutes(2);
                    }

                    if ((NextUpload < DateTime.UtcNow && OnDemand != null && !OnDemand.IsLocked) || 
                        (NextUpload < DateTime.UtcNow && OnDemand == null))
                    {
                        log.logMessage += $" Starting Data upload at " + DateTime.UtcNow.ToString() + "\n\n";
                        await d.DataUploadTask(SortOrder);
                        NextUpload = NextUpload.AddMinutes(dataupload.RunUploadEveryMins);
                        log.logMessage += $" Finished Data upload at " + DateTime.UtcNow.ToString() + " Next upload at- " + NextUpload.ToString() + "\n\n";
                        log.AddLogsToFile(DateTime.UtcNow);

                        SortOrder = SortOrder == -1 ? 1 : -1;
                    }
                }
                catch (Exception ex)
                {
                    log.logMessage += $"Error in data upload process {ex.Message}    {ex.StackTrace}";
                    log.AddLogsToFile(DateTime.UtcNow);
                    continue;
                }
            }

        }
    }
}
