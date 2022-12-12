using Microsoft.Extensions.Configuration;
using SFTPToS3Sync.Helper;
using System;
using System.IO;
using XM.ID.Net;

namespace SFTPToS3Sync.Domains
{
    /// <summary>
    /// Local folder connector.
    /// </summary>
    class LocalConnector : ILocalConnector
    {
        private readonly IS3Connector s3Connector;
        private readonly MongoDBConnector mongoDBConnector;
        public IConfiguration Configuration { get; }
        public readonly string baseDirectory;

        /// <summary>
        /// Initialize the settings.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="_s3Connector"></param>
        public LocalConnector(IConfiguration configuration, 
            IS3Connector _s3Connector, MongoDBConnector _mongoDBConnector)
        {
            Configuration = configuration;
            s3Connector = _s3Connector;
            mongoDBConnector = _mongoDBConnector;
            baseDirectory = configuration["Local:BaseDirectory"];
        }

        /// <summary>
        /// Move content from Local directory to S3.
        /// </summary>
        public void DownloadContent()
        {
            try
            {
                var directories = Directory.GetDirectories(baseDirectory);
                foreach (var directory in directories)
                {
                    var directoryInfo = new FileInfo(directory);
                    var allFiles = Directory.GetFiles(directory);
                    foreach (var file in allFiles)
                    {
                        // Upload indiviual file to S3.
                        bool result = s3Connector.UploadContent(file, directoryInfo.Name);
                        if (result && !file.EndsWith("config.json", StringComparison.OrdinalIgnoreCase))
                            File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                SSLM.logs.Add(mongoDBConnector.CreateLogEvent(SSLM.InternalException(ex)));
            }
        }
    }
}
