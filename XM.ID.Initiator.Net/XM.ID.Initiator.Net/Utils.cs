using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Initiator.Net
{
    internal static class Utils
    {
        internal static LogEvent CreateLogEvent(RequestPayload requestPayload, LogMessage logMessage)
        {
            var UtcNow = DateTime.UtcNow;
            return new LogEvent
            {
                DeliveryWorkFlowId = requestPayload.Dispatch?.DeliveryPlanId,
                Location = requestPayload.Dispatch?.QuestionnaireDisplayName,
                Created = UtcNow,
                Id = ObjectId.GenerateNewId().ToString(),
                LogMessage = logMessage,
                S3EventLog = requestPayload.S3EventLog,
                DispatchId = requestPayload.DispatchIdAndDispatchReqApi?.DispatchId,
                Tags = new List<string> { "Initiator" },
                Updated = UtcNow,
                User = Resources.GetInstance().AccountConfiguration.WXMAdminUser
            };
        }

        internal static LogEvent CreateLogEvent(RequestPayload requestPayload, LogMessage logMessage, string batchId)
        {
            var UtcNow = DateTime.UtcNow;
            return new LogEvent
            {
                BatchId = batchId,
                DeliveryWorkFlowId = requestPayload.Dispatch?.DeliveryPlanId,
                Location = requestPayload.Dispatch?.QuestionnaireDisplayName,
                Created = UtcNow,
                Id = ObjectId.GenerateNewId().ToString(),
                LogMessage = logMessage,
                S3EventLog = requestPayload.S3EventLog,
                DispatchId = requestPayload.DispatchIdAndDispatchReqApi?.DispatchId,
                Tags = new List<string> { "Initiator" },
                Updated = UtcNow,
                User = Resources.GetInstance().AccountConfiguration.WXMAdminUser
            };
        }

        internal static async Task<RequestInitiatorRecords> GetInitiatorRecordByFilename(string filename)
        {
            return await Resources.GetInstance().RequestInitiatorCollection.Find(x => x.FileName.Equals(filename))
                .SortByDescending(x => x.CreatedOn).FirstOrDefaultAsync(); 
        }

        internal static async Task InsertInitiatorRecordByFilename(string filename, string batchID,
            string numberofBatches, string displayFilename)
        {
            var record = new RequestInitiatorRecords
            {
                BatchId = batchID,
                FileName = filename,
                Id = ObjectId.GenerateNewId().ToString(),
                DisplayFileName = displayFilename,
                NoOfBatches = numberofBatches,
                CreatedOn = DateTime.UtcNow
            };
            await Resources.GetInstance().RequestInitiatorCollection.InsertOneAsync(record);
        }

        internal static async Task FlushLogs(RequestPayload requestPayload)
        {
            await Resources.GetInstance().LogEventCollection.InsertManyAsync(
                requestPayload.LogEvents.Where(x => x.LogMessage.IsLogInsertible(Resources.GetInstance().LogLevel))
                );
        }
    }
}
