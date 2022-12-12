using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XM.ID.Net;

namespace DispatcherEmailService.Helper
{
    public class ViaMongoDB
    {
        IMongoCollection<AccountConfiguration> _AccountConfiguration;
        public ViaMongoDB(IConfiguration _configuration)
        {
            MongoClientSettings settings = null;
            IMongoDatabase asyncdb = null;
            //var tenantProperties = _configuration.GetSection("TenantDetails").Get<List<Dictionary<string, string>>>();
            settings = MongoClientSettings.FromUrl(new MongoUrl(_configuration["MONGODB_URL"]));
            settings.MaxConnectionIdleTime = TimeSpan.FromMinutes(3);
            settings.ConnectTimeout = TimeSpan.FromSeconds(20);
            settings.MaxConnectionPoolSize = 1000;
            settings.ReadPreference = ReadPreference.Nearest;
            var mongoClient = new MongoClient(settings);
            asyncdb = mongoClient.GetDatabase(_configuration["DbNAME"]);
            _AccountConfiguration = asyncdb.GetCollection<AccountConfiguration>("AccountConfiguration");
        }

        public async Task<string> GetAccountConfiguration()
        {
            AccountConfiguration accountConfiguration = await _AccountConfiguration.Find(_ => true).FirstOrDefaultAsync();
            if (accountConfiguration != null)
                return JsonConvert.SerializeObject(accountConfiguration);
            else
                return null;
        }
    }
}
