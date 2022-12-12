using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using SFTPToS3Sync.Helper;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using XM.ID.Net;

namespace SFTPToS3Sync.Domains
{
    /// <summary>
    /// SFTP connector.
    /// </summary>
    class SFTPConnector : ISFTPConnector
    {
        private readonly IS3Connector s3Connector;
        private readonly MongoDBConnector mongoDBConnector;
        public IConfiguration Configuration { get; }
        private readonly string baseDirectory;
        private readonly string Url;
        private readonly int port;
        private readonly string username;
        private readonly string password;
        private SftpClient sftpClient;

        /// <summary>
        /// Initialize SFTP connector.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="_s3Connector"></param>
        public SFTPConnector(IConfiguration configuration,
            IS3Connector _s3Connector, MongoDBConnector _mongoDBConnector)
        {
            Configuration = configuration;
            s3Connector = _s3Connector;
            mongoDBConnector = _mongoDBConnector;
            baseDirectory = configuration["SFTP:BaseDirectory"];
            Url = configuration["SFTP:Url"];
            Int32.TryParse(configuration["SFTP:Port"], out port);
            username = configuration["SFTP:Username"];
            password = configuration["SFTP:Password"];
        }

        /// <summary>
        /// Move content from SFTP to S3.
        /// </summary>
        public void DownloadContent()
        {
            try
            {
                var retries = 1; 
                while (retries > 0)
                {
                    try
                    {
                        sftpClient = new SftpClient(Url, port, username, password);
                        sftpClient.Connect();
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("SFTP connection failed.");
                        if (retries > 3)
                        {
                            SSLM.logs.Add(mongoDBConnector.CreateLogEvent(SSLM.SFTPConnectivityUnsuccessful(ex)));
                            return;
                        }
                        else
                            retries++;
                        Thread.Sleep(10000); // 10 seconds delay
                    }
                }
                // Get list of files in the directory
                var filesandDir = sftpClient.ListDirectory(baseDirectory);
                var directories = filesandDir.Where(file => file.IsDirectory && file.Name != "." 
                                    && file.Name != "..").ToList();
                if (directories == null || directories.Count == 0)
                {
                    SSLM.logs.Add(mongoDBConnector.CreateLogEvent(SSLM.SFTPConnectivityUnsuccessful(new Exception("No Directory Found"))));
                    return;
                }

                foreach (var directory in directories)
                {
                    var files = sftpClient.ListDirectory(directory.FullName).Where(file => !file.IsDirectory).ToList();
                    string downloadFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "SFTPDownload", directory.Name);
                    if (!Directory.Exists(downloadFolderPath))
                        Directory.CreateDirectory(downloadFolderPath);
                    foreach (var file in files)
                    { 
                        string localFilePath = Path.Combine(downloadFolderPath, file.Name);
                        using Stream fileStream = File.OpenWrite(localFilePath);
                        sftpClient.DownloadFile(file.FullName, fileStream);
                        fileStream.Close();
                        // Upload file to S3 only for success
                        bool result = s3Connector.UploadContent(localFilePath, directory.Name);
                        if (result && !file.Name.Equals("config.json", StringComparison.OrdinalIgnoreCase))
                            sftpClient.DeleteFile(file.FullName);
                        File.Delete(localFilePath);
                    }
                    Directory.Delete(downloadFolderPath);
                }
                if (sftpClient.IsConnected)
                { 
                    sftpClient.Disconnect();
                }
                sftpClient.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Some exception occured in SFTP {ex}");
                SSLM.logs.Add(mongoDBConnector.CreateLogEvent(SSLM.InternalException(ex)));
            }
        }
    }
}
