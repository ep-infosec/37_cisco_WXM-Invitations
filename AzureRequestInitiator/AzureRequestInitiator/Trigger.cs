using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using XM.ID.Initiator.Net;
using XM.ID.Net;

namespace AzureRequestInitiator
{
    public class Trigger
    {
        private static string _dbConnectionString = Environment.GetEnvironmentVariable("MongoDBConnectionString");
        private static string _dbName = Environment.GetEnvironmentVariable("DatabaseName");
        private static string _awsAccessKeyId = Environment.GetEnvironmentVariable("AwsAccessKeyId");
        private static string _awsSecretAccessKey = Environment.GetEnvironmentVariable("AwsSecretAccessKey");
        private static string _region = Environment.GetEnvironmentVariable("Region");
        public IAmazonS3 S3Client { get; } = new AmazonS3Client(_awsAccessKeyId, _awsSecretAccessKey, Amazon.RegionEndpoint.GetBySystemName(_region));

        [FunctionName("S3EventTrigger")]
        public async Task RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string snsMessageType = req.Headers["x-amz-sns-message-type"][0];
            StreamReader request = new StreamReader(req.Body);
            JObject jsonRequest = JObject.Parse(request.ReadToEnd());

            if (snsMessageType == "SubscriptionConfirmation")
            {
                HttpClient httpClient = new HttpClient();
                string token = (string)jsonRequest["Token"];
                string subscribeUrl = (string)jsonRequest["SubscribeURL"];
                string confirmRequestSubscriptionUri = $"{subscribeUrl.Substring(0, subscribeUrl.LastIndexOf("&"))}&Token={token}";
                HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, confirmRequestSubscriptionUri);
                HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);
                if (!httpResponseMessage.IsSuccessStatusCode)
                    throw new Exception($"Couldn't Confirm Subscription. Reason => {await httpResponseMessage.Content.ReadAsStringAsync()}");
            }
            else if (snsMessageType == "Notification")
            {
                S3Event evnt = JsonConvert.DeserializeObject<S3Event>((string)jsonRequest["Message"]);
                S3EventHandler s3EventHandler = new S3EventHandler(_dbConnectionString, _dbName, S3Client, 5);
                foreach (S3EventNotification.S3EventNotificationRecord s3EventNotificationRecord in evnt.Records)
                {
                    S3EventLog s3EventLog = new S3EventLog
                    {
                        BucketName = s3EventNotificationRecord.S3.Bucket.Name,
                        EventName = s3EventNotificationRecord.EventName,
                        EventTime = s3EventNotificationRecord.EventTime,
                        KeyName = s3EventNotificationRecord.S3.Object.Key
                    };
                    await s3EventHandler.ConsumeS3Event(s3EventLog);
                }
            }
        }
    }
}
