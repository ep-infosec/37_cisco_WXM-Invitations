using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XM.ID.Dispatcher.Net;
using XM.ID.Dispatcher.Net.DispatchVendors;
using XM.ID.Net;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace AwsTimeTrigger
{
    public class Trigger
    {
        private static string _dbConnectionString = Environment.GetEnvironmentVariable("MongoDBConnectionString");
        private static string _dbName = Environment.GetEnvironmentVariable("DatabaseName");

        public async Task RunAsync(CloudWatchEvent<object> cloudWatchEvent, ILambdaContext context)
        {
            bool isLate;
            //For scheduled events of type (0/5 * * * * *) with a buffer of 10 secs
            if (cloudWatchEvent.Time.Minute % 5 == 0 && cloudWatchEvent.Time.Second < 11)
                isLate = false;
            else
                isLate = true;

            //Add your new vendor integration factory method here
            Dictionary<string, Func<IDispatchVendor>> additionalDispatchCreatorStrategies
                = new Dictionary<string, Func<IDispatchVendor>> { { "SampleBulkSendVendor", () => new SampleBulkSendVendor() } };

            //Pass necessary run-time settings here
            DispatchHandler dispatchHandler = new DispatchHandler(_dbConnectionString, _dbName, 5,
                additionalDispatchCreatorStrategies, "SparkPost", 10000);
            await dispatchHandler.ProcessMultipleMessage(isLate);
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
