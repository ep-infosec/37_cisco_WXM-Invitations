using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XM.ID.Net;

namespace SFTPToS3Sync.Helper
{
    class MongoDBConnector
    {
        internal MongoClient MongoClient { get; private set; }
        internal IMongoCollection<LogEvent> LogEventCollection { get; private set; }
        public IConfiguration Configuration { get; }
        public string Database { get; private set; }
        public int LogLevel { get; set; }

        public MongoDBConnector(IConfiguration configuration)
        {
            Configuration = configuration;
            int.TryParse(Configuration["MongoDB:LogLevel"], out int logLevel);
            #region MongoDB Management
            MongoClientSettings settings = MongoClientSettings.FromUrl(new MongoUrl(Configuration["MongoDB:ConnectionString"]));
            settings.MaxConnectionIdleTime = TimeSpan.FromMinutes(3);
            settings.ConnectTimeout = TimeSpan.FromSeconds(20);
            settings.MaxConnectionPoolSize = 1000;
            settings.ReadPreference = ReadPreference.Primary;
            MongoClient = new MongoClient(settings);
            Database = Configuration["MongoDB:DataBaseName"];

            LogEventCollection = MongoClient.GetDatabase(Database).GetCollection<LogEvent>("EventLog");

            #endregion

            LogLevel = logLevel < 1 ? 1 : logLevel;
        }

        internal async Task FlushLogs(List<LogEvent> logEvents)
        {
            await LogEventCollection.InsertManyAsync(
                logEvents.Where(x => x.LogMessage.IsLogInsertible(LogLevel))
                );
            logEvents.Clear();
        }

        public LogEvent CreateLogEvent(LogMessage logMessage)
        {
            var UtcNow = DateTime.UtcNow;
            return new LogEvent
            {
                BatchId = null,
                Created = UtcNow,
                DeliveryWorkFlowId = null,
                DispatchId = null,
                Events = null,
                Id = ObjectId.GenerateNewId().ToString(),
                Location = null,
                LogMessage = logMessage,
                Prefills = null,
                Tags = new List<string> { "S3Sync" },
                Target = null,
                TargetHashed = null,
                TokenId = null,
                Updated = UtcNow,
                User = null
            };
        }
    }
}
