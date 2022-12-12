using Amazon.S3;
using System;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Initiator.Net
{
    public class S3EventHandler
    {
        /// <summary>
        /// This constructor will allow you to set-up the necessary run-time details.
        /// Use this to provide the required Database Details and IAmazonS3 instance
        /// along with other optional settings such as LogLevel.
        /// </summary>
        /// <param name="mongoDbConnectionString"></param>
        /// <param name="databaseName"></param>
        /// <param name="s3Client"></param>
        /// <param name="logLevel"></param>
        public S3EventHandler(string mongoDbConnectionString,
            string databaseName,
            IAmazonS3 s3Client,
            int logLevel = 5)
        {
            Resources.GetOrCreateInstance(mongoDbConnectionString, databaseName, s3Client, logLevel);
        }

        public async Task ConsumeS3Event(S3EventLog s3EventLog)
        {
            RequestPayload requestPayload = new RequestPayload(s3EventLog);
            try
            {
                if (!requestPayload.IsTargetFileUploadDirectoryValid)
                    return;

                await requestPayload.FetchConfigFile();
                if (!requestPayload.IsConfigFileAvailableAndNotEmpty)
                    return;

                await requestPayload.FetchTargetFile();
                if (!requestPayload.IsTargetFileAvailableAndNotEmpty)
                    return;

                await requestPayload.SplitFileInBatches();
                if (requestPayload.IsFileSplitted)
                    return;

                requestPayload.ValidateConfigFile();
                if (!requestPayload.IsConfigFileValid)
                    return;

                await requestPayload.FetchBearerToken();
                if (!requestPayload.IsLoginPossible)
                    return;

                await requestPayload.ValidateDispatch();
                if (!requestPayload.IsDispatchValid)
                    return;

                requestPayload.ValidateTargetFileHeaders();
                if (!requestPayload.IsTargetFileHeaderWiseValid)
                    return;

                requestPayload.ValidateTargetFileRows();
                if (!requestPayload.IsTargetFileRowWiseValid)
                    return;

                await requestPayload.RequestDispatch();
            }
            catch (Exception ex)
            {
                requestPayload.LogEvents.Add(Utils.CreateLogEvent(requestPayload, IRILM.InternalException(ex)));
            }
            finally
            {
                if (requestPayload.IsTargetFileUploadDirectoryValid && !requestPayload.IsFileSplitted)
                    await requestPayload.ArchiveTargetFile();
                await Utils.FlushLogs(requestPayload);
            }
        }
    }
}
