using MongoDB.Bson;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Invitations.Net
{
    public class EventLogList
    {
        private readonly List<LogEvent> logEvents;

        public EventLogList()
        {
            logEvents = new List<LogEvent>();
        }

        public void AddEventByLevel(int level, string message, string batchId, string dispatchId = null, string deliveryPlanId = null, string questionnaire = null)
        {
            string CriticalityLevel;
            if (level == 1)
                CriticalityLevel = LogMessage.SeverityLevel_Critical;
            else if (level == 2)
                CriticalityLevel = LogMessage.SeverityLevel_Error;
            else if (level == 3)
                CriticalityLevel = LogMessage.SeverityLevel_Information;
            else if (level == 4)
                CriticalityLevel = LogMessage.SeverityLevel_Warning;
            else
                CriticalityLevel = LogMessage.SeverityLevel_Verbose;

            var logEvent = new LogEvent()
            {
                BatchId = batchId,
                DispatchId = dispatchId,
                DeliveryWorkFlowId = deliveryPlanId,
                Location = questionnaire,
                LogMessage = new LogMessage() { Message = message, Level = CriticalityLevel },
                Tags = new List<string> { "DispatchAPI" }
            };
            logEvent.Id = ObjectId.GenerateNewId().ToString();
            logEvent.Created = DateTime.UtcNow;
            logEvents.Add(logEvent);
        }

        public void AddExceptionEvent(Exception ex0, string batchId = null, string dispatchId = null, string deliveryPlanId = null, string questionnaire = null, string message = null)
        {
            List<string> tags = new List<string>();
            if (string.IsNullOrWhiteSpace(dispatchId))
                tags.Add("Account");
            else          
                tags.Add("DispatchAPI");
            

            var logEvent = new LogEvent()
            {
                BatchId = batchId,
                DispatchId = dispatchId,
                DeliveryWorkFlowId = deliveryPlanId,
                Location = questionnaire,
                LogMessage = new LogMessage() { Exception = JsonConvert.SerializeObject(ex0), Level = LogMessage.SeverityLevel_Critical, Message = message },
                Tags = tags

            };
            logEvent.Id = ObjectId.GenerateNewId().ToString();
            logEvent.Created = DateTime.UtcNow;
            logEvents.Add(logEvent);
        }

        public async Task AddEventLogs(ViaMongoDB via)
        {
            await via.AddLogEvents(logEvents);
            logEvents.Clear();
        }
    }
}
