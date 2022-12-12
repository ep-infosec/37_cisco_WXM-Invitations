using Amazon.S3;
using MongoDB.Driver;
using System;
using XM.ID.Net;

namespace XM.ID.Initiator.Net
{
    public sealed class Resources
    {
        internal MongoClient MongoClient { get; private set; }
        internal IMongoCollection<AccountConfiguration> ConfigCollection { get; private set; }
        internal IMongoCollection<LogEvent> LogEventCollection { get; private set; }
        internal IMongoCollection<RequestInitiatorRecords> RequestInitiatorCollection { get; private set; }
        internal AccountConfiguration AccountConfiguration { get; private set; }

        public IAmazonS3 S3Client { get; private set; }
        internal WXMService WXMService { get; private set; }
        public int LogLevel { get; set; }

        private static Resources _instance = null;
        private static readonly object resourceLock = new object();

        private Resources() { }

        private Resources(string mongoDbConnectionString,
            string databaseName,
            IAmazonS3 s3Client,
            int logLevel = 5)
        {
            #region MongoDB Management
            MongoClientSettings settings = MongoClientSettings.FromUrl(new MongoUrl(mongoDbConnectionString));
            settings.MaxConnectionIdleTime = TimeSpan.FromMinutes(3);
            settings.ConnectTimeout = TimeSpan.FromSeconds(20);
            settings.MaxConnectionPoolSize = 1000;
            settings.ReadPreference = ReadPreference.Primary;
            MongoClient = new MongoClient(settings);

            ConfigCollection = MongoClient.GetDatabase(databaseName).GetCollection<AccountConfiguration>("AccountConfiguration");
            LogEventCollection = MongoClient.GetDatabase(databaseName).GetCollection<LogEvent>("EventLog");
            RequestInitiatorCollection = MongoClient.GetDatabase(databaseName).GetCollection<RequestInitiatorRecords>("RequestInitiatorRecords");

            AccountConfiguration = ConfigCollection.Find(_ => true).FirstOrDefault();
            if (AccountConfiguration == default)
            {
                throw new MissingMemberException("Account-Configuration wasn't found");
            }
            #endregion

            #region Miscellaneous Management
            S3Client = s3Client;
            WXMService = new WXMService(AccountConfiguration.WXMBaseURL);
            LogLevel = logLevel < 1 ? 1 : logLevel;
            #endregion
        }

        private static Resources CreateSingleton(string mongoDbConnectionString,
            string databaseName,
            IAmazonS3 s3Client,
            int logLevel = 5)
        {
            try
            {
                _instance = new Resources(mongoDbConnectionString, databaseName, s3Client, logLevel);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw ex;
            }
            return _instance;
        }

        public static Resources GetOrCreateInstance(string mongoDbConnectionString,
            string databaseName,
            IAmazonS3 s3Client,
            int logLevel = 5)
        {
            if (_instance != null)
            {
                return _instance;
            }
            else
            {
                lock (resourceLock)
                {
                    return _instance is null
                        ? CreateSingleton(mongoDbConnectionString, databaseName, s3Client, logLevel)
                        : _instance;
                }
            }
        }

        public static Resources GetInstance()
        {
            //TO-DO: Handle the null scenario without throwing exception
            return _instance ?? throw new InvalidOperationException("Resources hasn't been initialized");
        }
    }
}
