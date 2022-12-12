using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XM.ID.Dispatcher.Net;
using XM.ID.Dispatcher.Net.DispatchVendors;
using XM.ID.Net;

namespace AzureQueueTrigger
{
    public class Trigger
    {
        private static string _dbConnectionString = Environment.GetEnvironmentVariable("MongoDBConnectionString");
        private static string _dbName = Environment.GetEnvironmentVariable("DatabaseName");

        [FunctionName("QueueTrigger")]
        public async Task RunAsync([QueueTrigger("%QueueName%")] QueueData queueData, ILogger log)
        {
            //Add your new vendor integration factory method here
            Dictionary<string, Func<IDispatchVendor>> additionalDispatchCreatorStrategies
                = new Dictionary<string, Func<IDispatchVendor>> { { "SampleSingleSendVendor", () => new SampleSingleSendVendor() } };
            
            //Pass necessary run-time settings here
            DispatchHandler dispatchHandler = new DispatchHandler(_dbConnectionString, _dbName, 5,
                additionalDispatchCreatorStrategies, string.Empty, int.MinValue);
            await dispatchHandler.ProcessSingleMessage(queueData);
        }
    }

    //Reference Integration example for a single-send vendor
    class SampleSingleSendVendor : ISingleDispatchVendor
    {
        public Vendor Vendor { get; set; }

        public void Setup(Vendor vendor)
        {
            Vendor = vendor;
        }

        public async Task RunAsync(MessagePayload messagePayload)
        {
            /*
             * Your implementation logic goes here. For more details refer to source code's already
             * implemented single-send vendors such as CustomSMTP and MessageBird
             */
            await Task.CompletedTask;
            return;
        }
    }
}
