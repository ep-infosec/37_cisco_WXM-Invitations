using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using SFTPToS3Sync.Helper;
using System;
using System.IO;
using XM.ID.Net;

namespace SFTPToS3Sync.Domains
{
    /// <summary>
    /// S3 Connector
    /// </summary>
    class S3Connector : IS3Connector
    {
        public IConfiguration Configuration { get; }
        private readonly MongoDBConnector mongoDBConnector;
        private readonly string baseDirectory;
        private readonly string bucketName;
        private readonly bool serverdeployment;
        private readonly string awsaccesskeyID;
        private readonly string awssecretaccessKey;
        private readonly RegionEndpoint bucketRegion;
        private readonly IAmazonS3 s3Client;

        /// <summary>
        /// Initialize the settings.
        /// </summary>
        /// <param name="configuration"></param>
        public S3Connector(IConfiguration configuration, MongoDBConnector _mongoDBConnector)
        {
            Configuration = configuration;
            mongoDBConnector = _mongoDBConnector;
            baseDirectory = configuration["S3:BaseDirectory"];
            bucketName = configuration["S3:BucketName"];
            bool.TryParse(configuration["S3:IsServerDeployment"], out serverdeployment);
            awsaccesskeyID = configuration["S3:AWSAccessKeyId"];
            awssecretaccessKey = configuration["S3:AWSSecretAccessKey"];
            bucketRegion = RegionEndpoint.GetBySystemName(configuration["S3:BucketRegionCode"]);

            //
            if (serverdeployment)
                s3Client = new AmazonS3Client(bucketRegion);
            else
                s3Client = new AmazonS3Client(awsaccesskeyID, awssecretaccessKey, bucketRegion);
        }

        /// <summary>
        /// Upload file to S3 bucket
        /// </summary>
        /// <param name="sOutputFile">Full local file path</param>
        /// <param name="directoryName">Directory in which resides.</param>
        /// <returns></returns>
        public bool UploadContent(string sOutputFile, string directoryName)
        {
            var tries = 1;
            while (tries > 0)
            {
                try
                {
                    var fileTransferUtility = new TransferUtility(s3Client);
                    string s3outputlocation = baseDirectory;
                    if (s3outputlocation != null && s3outputlocation.Length > 0 && s3outputlocation[s3outputlocation.Length - 1] != '/')
                        s3outputlocation += "/";
                    string fileToBeCopied = s3outputlocation + directoryName + "/" +
                                Path.GetFileName(sOutputFile);
                    var fileTransferUtilityRequest = new TransferUtilityUploadRequest
                    {
                        BucketName = bucketName,
                        FilePath = sOutputFile,
                        Key = fileToBeCopied,
                        StorageClass = S3StorageClass.StandardInfrequentAccess,
                        PartSize = 6291456, // 6 MB.
                        CannedACL = S3CannedACL.NoACL
                    };

                    fileTransferUtility.UploadAsync(fileTransferUtilityRequest).Wait();
                    SSLM.logs.Add(mongoDBConnector.CreateLogEvent(SSLM.FileInfo(sOutputFile)));
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Upload Failed {ex}");
                    SSLM.logs.Add(mongoDBConnector.CreateLogEvent(SSLM.InternalException(ex)));
                    if (tries > 3)
                        return false;
                    else
                        tries++;
                }
            }
            return false;
        }
    }
}
