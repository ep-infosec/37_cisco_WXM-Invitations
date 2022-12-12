using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XM.ID.Dispatcher.Net;
using XM.ID.Dispatcher.Net.DispatchVendors;
using XM.ID.Net;

namespace AzureTimeTrigger
{
    public class Trigger
    {
        private static string _dbConnectionString = Environment.GetEnvironmentVariable("MongoDBConnectionString");
        private static string _dbName = Environment.GetEnvironmentVariable("DatabaseName");

        [FunctionName("TimeTrigger")]
        public async Task RunAsync([TimerTrigger("0 */5 * * * *", RunOnStartup = false)]TimerInfo myTimer, ILogger log)
        {
            //Add your new vendor integration factory method here
            Dictionary<string, Func<IDispatchVendor>> additionalDispatchCreatorStrategies
                = new Dictionary<string, Func<IDispatchVendor>> { { "SampleBulkSendVendor", () => new SampleBulkSendVendor() } };
            
            //Pass necessary run-time settings here
            DispatchHandler dispatchHandler = new DispatchHandler(_dbConnectionString, _dbName, 5, 
                additionalDispatchCreatorStrategies, "SparkPost", 10000);
            await dispatchHandler.ProcessMultipleMessage(myTimer.IsPastDue);
        }
    }

    //Reference Integration example for a bulk-send vendor
    class SampleBulkSendVendor : IBulkDispatchVendor
    {
        public Vendor Vendor { get; set; }

        public void Setup(Vendor vendor)
        {
            Vendor = vendor;
        }

        public async Task RunAsync(List<MessagePayload> messagePayloads)
        {
            /*
             * Your implementation logic goes here. For more details refer to source code's already
             * implemented bulk-send vendors such as SparkPost
             */
            await Task.CompletedTask;
            return;
        }
    }
}
