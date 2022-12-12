using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Util;
using System;
using System.Threading.Tasks;
using XM.ID.Initiator.Net;
using XM.ID.Net;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace AwsRequestInitiator
{
    public class Trigger
    {
        public IAmazonS3 S3Client { get; } = new AmazonS3Client();
        private static string _dbConnectionString = Environment.GetEnvironmentVariable("MongoDBConnectionString");
        private static string _dbName = Environment.GetEnvironmentVariable("DatabaseName");

        public async Task RunAsync(S3Event evnt, ILambdaContext context)
        {
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
