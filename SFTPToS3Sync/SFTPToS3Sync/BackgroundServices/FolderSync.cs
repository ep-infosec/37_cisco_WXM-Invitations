using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NCrontab;
using SFTPToS3Sync.Domains;
using SFTPToS3Sync.Helper;
using System;
using System.Threading;
using System.Threading.Tasks;
using XM.ID.Net;

/// <summary>
/// 
/// </summary>
namespace SFTPToS3Sync.BackgroundServices
{
    /// <summary>
    /// Backgroung Hosted service.
    /// </summary>
    class FolderSync : BackgroundService
    {
        public IConfiguration Configuration { get; }
        public MongoDBConnector MongoDBConnector { get; set; }
        private readonly ILocalConnector localConnector;
        private readonly ISFTPConnector sFTPConnector;
        private CrontabSchedule _schedule;
        private DateTime _nextRun;
        private string Schedule;

        /// <summary>
        /// Initialize cron job
        /// </summary>
        /// <param name="_localConnector"></param>
        /// <param name="_sFTPConnector"></param>
        /// <param name="_configuration"></param>
        public FolderSync(ILocalConnector _localConnector, ISFTPConnector _sFTPConnector, 
            IConfiguration _configuration, MongoDBConnector mongoDBConnector)
        {
            Configuration = _configuration;
            localConnector = _localConnector;
            sFTPConnector = _sFTPConnector;
            MongoDBConnector = mongoDBConnector;

            Schedule = (Configuration["SyncFrequency"]) switch
            {
                "1" => @"0 * * * * *",
                "2" => @"0 */2 * * * *",
                "5" => @"0 */5 * * * *",
                "10" => @"0 */10 * * * *",
                _ => @"0 */10 * * * *",
            };
            _schedule = CrontabSchedule.Parse(Schedule, new CrontabSchedule.ParseOptions { IncludingSeconds = true });
            _nextRun = DateTime.Now;

        }

        /// <summary>
        /// Execute function.
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            do
            {
                var now = DateTime.Now;
                if (now > _nextRun)
                {
                    SyncProcess();
                    await MongoDBConnector.FlushLogs(SSLM.logs);
                    _nextRun = _schedule.GetNextOccurrence(DateTime.Now);
                }
                await Task.Delay(5000, stoppingToken); // 5 seconds delay
            }
            while (!stoppingToken.IsCancellationRequested);

        }

        /// <summary>
        /// SFTP/Local to S3 sync
        /// </summary>
        private void SyncProcess()
        {
            var sendingConnector = Configuration["ConnectorType"];
            SSLM.logs.Add(MongoDBConnector.CreateLogEvent(SSLM.Connector(sendingConnector)));
            if (sendingConnector.Equals("Local", StringComparison.OrdinalIgnoreCase))
            {
                localConnector.DownloadContent();
                SSLM.logs.Add(MongoDBConnector.CreateLogEvent(SSLM.LocalSync));
            }
            else if (sendingConnector.Equals("SFTP", StringComparison.OrdinalIgnoreCase))
            {
                sFTPConnector.DownloadContent();
                SSLM.logs.Add(MongoDBConnector.CreateLogEvent(SSLM.SFTPSync));
            }
            else
            {
                SSLM.logs.Add(MongoDBConnector.CreateLogEvent(SSLM.NoConnectorFound));
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Stopping service");
            await base.StopAsync(cancellationToken);
            SSLM.logs.Add(MongoDBConnector.CreateLogEvent(SSLM.ApplicatonStopped));
        }
    }
}
