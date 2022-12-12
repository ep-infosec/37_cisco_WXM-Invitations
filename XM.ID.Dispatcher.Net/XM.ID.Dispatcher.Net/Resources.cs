using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using XM.ID.Dispatcher.Net.DispatchVendors;
using XM.ID.Net;

namespace XM.ID.Dispatcher.Net
{
    public sealed class Resources
    {
        internal MongoClient MongoClient { get; private set; }
        internal IMongoCollection<AccountConfiguration> ConfigCollection { get; private set; }
        internal IMongoCollection<LogEvent> LogEventCollection { get; private set; }
        internal IMongoCollection<DB_MessagePayload> BulkMessagePayloadCollection { get; private set; }
        internal AccountConfiguration AccountConfiguration { get; private set; }

        public HttpClient HttpClient { get; private set; }
        public object SmtpLock { get; private set; }

        public Dictionary<string, Func<IDispatchVendor>> DispatchReadyVendor_CreationStrategies { get; set; } = new Dictionary<string, Func<IDispatchVendor>>(StringComparer.InvariantCultureIgnoreCase);
        public int BulkReadSize { get; set; }
        public string BulkVendorName { get; set; }
        public int LogLevel { get; set; }
        public string SurveyBaseDomain { get; set; }
        public string UnsubscribeBaseUrl { get; set; }

        private static Resources _instance = null;
        private static readonly object resourceLock = new object();

        private Resources() { }

        private Resources(string mongoDbConnectionString,
            string databaseName,
            int logLevel = 5,
            Dictionary<string, Func<IDispatchVendor>> additionalDispatchCreatorStrategies = default,
            string bulkVendorName = "sparkpost",
            int bulkReadSize = 10000,
            string surveyBaseDomain = "nps.bz",
            string unsubscribeUrl = "https://cx.getcloudcherry.com/l/unsub/?token=")
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
            BulkMessagePayloadCollection = MongoClient.GetDatabase(databaseName).GetCollection<DB_MessagePayload>("BulkMessage");

            AccountConfiguration = ConfigCollection.Find(_ => true).FirstOrDefault();
            if (AccountConfiguration == default)
            {
                throw new MissingMemberException("Account-Configuration wasn't found");
            }
            #endregion

            #region Miscellaneous Management
            HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(1)
            };
            SmtpLock = new object();
            LogLevel = logLevel < 1 ? 1 : logLevel;
            DispatchReadyVendor_CreationStrategies = new Dictionary<string, Func<IDispatchVendor>>()
            {
                { "customsmtp", () => new CustomSMTP() },
                { "messagebird", () => new MessageBird() },
                { "sparkpost", () => new SparkPost() },
                { "customsms", () => new CustomSMS() },
                { "pinnacle", () => new Pinnacle() },
                { "vfsms", () => new ValueFirstSMS() }
            };
            if (default != additionalDispatchCreatorStrategies)
            {
                foreach (var kvp in additionalDispatchCreatorStrategies)
                {
                    DispatchReadyVendor_CreationStrategies.Add(kvp.Key, kvp.Value);
                }
            }
            BulkVendorName = bulkVendorName;
            BulkReadSize = bulkReadSize < 0 ? 0 : bulkReadSize;
            SurveyBaseDomain = surveyBaseDomain;
            UnsubscribeBaseUrl = unsubscribeUrl;
            #endregion

        }

        private static Resources CreateSingleton(string mongoDbConnectionString,
            string databaseName,
            int logLevel = 5,
            Dictionary<string, Func<IDispatchVendor>> additionalDispatchCreatorStrategies = default,
            string bulkVendorName = "sparkpost",
            int bulkReadSize = 10000,
            string surveyBaseDomain = "nps.bz",
            string unsubscribeUrl = "https://cx.getcloudcherry.com/l/unsub/?token=")
        {
            try
            {
                _instance = new Resources(mongoDbConnectionString, databaseName, logLevel, additionalDispatchCreatorStrategies,
                    bulkVendorName, bulkReadSize, surveyBaseDomain, unsubscribeUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Utils.FlushLogs(new List<LogEvent> { Utils.CreateLogEvent(null, IRDLM.InternalException(ex)) }).GetAwaiter().GetResult();
                throw ex;
            }
            return _instance;
        }

        public static Resources GetOrCreateInstance(string mongoDbConnectionString,
            string databaseName,
            int logLevel = 5,
            Dictionary<string, Func<IDispatchVendor>> additionalDispatchCreatorStrategies = default,
            string bulkVendorName = "sparkpost",
            int bulkReadSize = 10000,
            string surveyBaseDomain = "nps.bz",
            string unsubscribeUrl = "https://cx.getcloudcherry.com/l/unsub/?token=")
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
                        ? CreateSingleton(mongoDbConnectionString, databaseName, logLevel, additionalDispatchCreatorStrategies,
                        bulkVendorName, bulkReadSize, surveyBaseDomain, unsubscribeUrl)
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
