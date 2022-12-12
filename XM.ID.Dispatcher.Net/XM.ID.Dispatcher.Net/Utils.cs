using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Dispatcher.Net
{
    public static class Utils
    {
        #region Logging-Related
        /// <summary>
        /// Creates a Log-Event to capture application-level + invitation-level logs
        /// </summary>
        /// <param name="queueData"></param>
        /// <param name="logMessage"></param>
        /// <returns></returns>
        public static LogEvent CreateLogEvent(QueueData queueData, LogMessage logMessage)
        {
            var UtcNow = DateTime.UtcNow;
            return new LogEvent
            {
                BatchId = queueData?.BatchId,
                Created = UtcNow,
                DeliveryWorkFlowId = null,
                DispatchId = queueData?.DispatchId,
                Events = null,
                Id = ObjectId.GenerateNewId().ToString(),
                Location = null,
                LogMessage = logMessage,
                Prefills = queueData?.MappedValue?.ToList()?.Select(x => new Prefill { QuestionId = x.Key, Input = null, Input_Hash = x.Value })?.ToList(),
                Tags = new List<string> { "Dispatcher" },
                Target = null,
                TargetHashed = queueData?.CommonIdentifier,
                TokenId = queueData?.TokenId,
                Updated = UtcNow,
                User = queueData?.User
            };
        }

        /// <summary>
        /// Create an Invitation-Log-Event to capture a unique stage in an Invitation's Journey
        /// </summary>
        /// <param name="eventAction"></param>
        /// <param name="eventChannel"></param>
        /// <param name="queueData"></param>
        /// <param name="logMessage"></param>
        /// <returns></returns>
        public static InvitationLogEvent CreateInvitationLogEvent(EventAction eventAction, EventChannel eventChannel, QueueData queueData, LogMessage logMessage)
        {
            return new InvitationLogEvent
            {
                Action = eventAction,
                Channel = eventChannel,
                EventStatus = null,
                LogMessage = logMessage,
                Message = $"Message Template Id: {queueData?.TemplateId} | Additional Token Parameters: {queueData.AdditionalURLParameter}",
                TargetId = eventChannel == EventChannel.Email ? queueData?.EmailId :
                eventChannel == EventChannel.SMS ? queueData?.MobileNumber : eventChannel.ToString(),
                TimeStamp = DateTime.UtcNow
            };
        }

        internal static async Task FlushLogs(List<MessagePayload> messagePayloads)
        {
            try
            {
                List<LogEvent> logEvents = new List<LogEvent>();
                messagePayloads.ForEach(x => logEvents.AddRange(
                    x.LogEvents.Where(y => y.LogMessage.IsLogInsertible(Resources.GetInstance().LogLevel))
                    ));

                if (logEvents.Count > 0)
                    await Resources.GetInstance().LogEventCollection.InsertManyAsync(logEvents);

                var filterBuilder = Builders<LogEvent>.Filter;
                var updateBuilder = Builders<LogEvent>.Update;
                var writeBuilder = new List<WriteModel<LogEvent>>();
                foreach (MessagePayload messagePayload in messagePayloads)
                {
                    if (messagePayload.InvitationLogEvents.Count > 0)
                    {
                        var filter = filterBuilder.Eq(x => x.Id, messagePayload.Invitation.Id);
                        var update = updateBuilder.PushEach(x => x.Events, messagePayload.InvitationLogEvents).Set(x => x.Updated, DateTime.UtcNow);
                        writeBuilder.Add(new UpdateOneModel<LogEvent>(filter, update));
                    }
                }
                if (writeBuilder.Count > 0)
                    await Resources.GetInstance().LogEventCollection.BulkWriteAsync(writeBuilder);
            }
            catch (Exception ex)
            {
                await FlushLogs(new List<LogEvent> { CreateLogEvent(null, IRDLM.InternalException(ex)) });
            }

        }

        internal static async Task FlushLogs(List<LogEvent> logEvents)
        {
            await Resources.GetInstance().LogEventCollection.InsertManyAsync(
                logEvents.Where(x => x.LogMessage.IsLogInsertible(Resources.GetInstance().LogLevel))
                );
        }
        #endregion

        #region BulkMessagePayloadManagement
        internal static async Task InsertBulkMessagePayload(MessagePayload messagePayload)
        {
            try
            {
                DB_MessagePayload dB_MessagePayload = new DB_MessagePayload(messagePayload);
                await Resources.GetInstance().BulkMessagePayloadCollection.InsertOneAsync(dB_MessagePayload);
            }
            catch (Exception ex)
            {
                await FlushLogs(new List<LogEvent> { CreateLogEvent(null, IRDLM.InternalException(ex)) });
            }
        }

        internal static async Task<List<DB_MessagePayload>> ReadBulkMessagePayloads()
        {
            try
            {
                return await Resources.GetInstance().BulkMessagePayloadCollection
                .Find(x => x.Status == "Ready")
                .Limit(Resources.GetInstance().BulkReadSize)
                .ToListAsync();
            }
            catch (Exception ex)
            {
                await FlushLogs(new List<LogEvent> { CreateLogEvent(null, IRDLM.InternalException(ex)) });
                return new List<DB_MessagePayload>();
            }
        }

        internal static async Task UpdateBulkMessagePayloads(List<DB_MessagePayload> dB_MessagePayloads)
        {
            try
            {
                List<string> docIds = dB_MessagePayloads.Select(x => x.Id).ToList();
                var filter = Builders<DB_MessagePayload>.Filter.In(x => x.Id, docIds);
                var update = Builders<DB_MessagePayload>.Update.Set(x => x.Status, "Processing");
                await Resources.GetInstance().BulkMessagePayloadCollection.UpdateManyAsync(filter, update);
            }
            catch (Exception ex)
            {
                await FlushLogs(new List<LogEvent> { CreateLogEvent(null, IRDLM.InternalException(ex)) });
            }
        }

        internal static async Task DeleteBulkMessagePayloads(List<DB_MessagePayload> dB_MessagePayloads)
        {
            try
            {
                List<string> docIds = dB_MessagePayloads.Select(x => x.Id).ToList();
                await Resources.GetInstance().BulkMessagePayloadCollection.DeleteManyAsync(x => docIds.Contains(x.Id));
            }
            catch (Exception ex)
            {
                await FlushLogs(new List<LogEvent> { CreateLogEvent(null, IRDLM.InternalException(ex)) });
            }
        }
        #endregion

        #region Hash-Look-Up
        /// <summary>
        /// Performs Hash-Look-Ups for PII Data if the received 
        /// QueueData-Object's Subject/Text-Body/Html-Body
        /// utilizes WXM Tag-Substitution.
        /// </summary>
        /// <param name="queueData"></param>
        public static void PerformLookUps(QueueData queueData)
        {
            try
            {
                string matchString = @"\$(\w+)\*\|(.*?)\|\*";

                //Subject
                if (!string.IsNullOrWhiteSpace(queueData.Subject))
                {
                    StringBuilder newSubject = new StringBuilder(queueData.Subject);
                    foreach (Match m in Regex.Matches(queueData.Subject, matchString, RegexOptions.Multiline))
                    {
                        string qid = m.Groups[1].Value;
                        string defaultValue = m.Groups[2].Value;
                        if (queueData.MappedValue.ContainsKey(qid))
                            newSubject.Replace(m.Groups[0].Value, queueData.MappedValue[qid]);
                        else
                            newSubject.Replace(m.Groups[0].Value, defaultValue);
                    }
                    queueData.Subject = newSubject.ToString();
                }

                //HTML Body
                if (!string.IsNullOrWhiteSpace(queueData.HTMLBody))
                {
                    StringBuilder newHtmlBody = new StringBuilder(queueData.HTMLBody);
                    foreach (Match m in Regex.Matches(queueData.HTMLBody, matchString, RegexOptions.Multiline))
                    {
                        string qid = m.Groups[1].Value;
                        string defaultValue = m.Groups[2].Value;
                        if (queueData.MappedValue.ContainsKey(qid))
                            newHtmlBody.Replace(m.Groups[0].Value, queueData.MappedValue[qid]);
                        else
                            newHtmlBody.Replace(m.Groups[0].Value, defaultValue);
                    }
                    queueData.HTMLBody = newHtmlBody.ToString();
                }

                //Text Body
                if (!string.IsNullOrWhiteSpace(queueData.TextBody))
                {
                    StringBuilder newTextBody = new StringBuilder(queueData.TextBody);
                    foreach (Match m in Regex.Matches(queueData.TextBody, matchString, RegexOptions.Multiline))
                    {
                        string qid = m.Groups[1].Value;
                        string defaultValue = m.Groups[2].Value;
                        if (queueData.MappedValue.ContainsKey(qid))
                            newTextBody.Replace(m.Groups[0].Value, queueData.MappedValue[qid]);
                        else
                            newTextBody.Replace(m.Groups[0].Value, defaultValue);
                    }
                    queueData.TextBody = newTextBody.ToString();
                }

            }
            catch (Exception ex)
            {
                FlushLogs(new List<LogEvent> { CreateLogEvent(null, IRDLM.InternalException(ex)) }).GetAwaiter().GetResult();
            }
        }
        #endregion

        #region Misc
        /// <summary>
        /// Generate a Survey-URL using the Token-Number & Additional-Parameters of the queue message and the configured Survey-Base-Domain
        /// </summary>
        /// <param name="queueData"></param>
        /// <returns>Token-Number & Additional-Parameters specific Survey-URL</returns>
        public static string GetSurveyURL(QueueData queueData)
        {
            return $"http://{Resources.GetInstance().SurveyBaseDomain}/{queueData.TokenId}{queueData.AdditionalURLParameter}";
        }

        /// <summary>
        /// Generate an Unsubscribe-URL using the Token-Number of the queue message
        /// </summary>
        /// <param name="queueData"></param>
        /// <returns>Token-Number specific Unsubscribe-URL</returns>
        public static string GetUnsubscribeURL(QueueData queueData)
        {
            return $"{Resources.GetInstance().UnsubscribeBaseUrl}{queueData.TokenId}";
        }

        /// <summary>
        /// To encode TextBody for YESBNK with proper values.
        /// </summary>
        /// <param name="TextBody">SMS body.</param>
        /// <returns></returns>
        public static string EncodeTextBody(string TextBody)
        {
            return TextBody.Replace("&", "amp;").Replace("#", ";hash").Replace("+", "plus;").Replace(",", "comma;");
        }
        #endregion
    }
}
