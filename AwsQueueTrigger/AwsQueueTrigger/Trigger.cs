using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using XM.ID.Dispatcher.Net;
using XM.ID.Dispatcher.Net.DispatchVendors;
using XM.ID.Net;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace AwsQueueTrigger
{
    public class Trigger
    {
        private static string _dbConnectionString = Environment.GetEnvironmentVariable("MongoDBConnectionString");
        private static string _dbName = Environment.GetEnvironmentVariable("DatabaseName");

        public async Task RunAsync(SQSEvent evnt, ILambdaContext context)
        {
            List<Task> tasks = new List<Task>();
            foreach (var message in evnt.Records)
            {
                //Add your new vendor integration factory method here
                Dictionary<string, Func<IDispatchVendor>> additionalDispatchCreatorStrategies
                    = new Dictionary<string, Func<IDispatchVendor>> { { "SampleSingleSendVendor", () => new SampleSingleSendVendor() } };

                //Pass necessary run-time settings here
                DispatchHandler dispatchHandler = new DispatchHandler(_dbConnectionString, _dbName, 5,
                    additionalDispatchCreatorStrategies, string.Empty, int.MinValue);

                //extracting
                var base64decoded = Convert.FromBase64String(message.Body);
                var jsonString = Encoding.UTF8.GetString(base64decoded);
                QueueData queueData = JsonConvert.DeserializeObject<QueueData>(jsonString);
                tasks.Add(dispatchHandler.ProcessSingleMessage(queueData));
            }
            await Task.WhenAll(tasks);
        }

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
}