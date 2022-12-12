using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XM.ID.Dispatcher.Net.DispatchVendors;
using XM.ID.Net;

namespace XM.ID.Dispatcher.Net
{
    public class DispatchHandler
    {
        /// <summary>
        /// This constructor will allow you to set-up the necessary run-time details.
        /// Use this to provide the required Database Details along with other optional
        /// settings such as LogLevel, Factory Methods for custom Vendor Integrations,
        /// BulkVendorName, BulkReadSize, SurveyBaseDomain, UnsubscribeURL. BulkVendorName,
        /// BulkReadSize are required for running Time-Trigger Serverless Computes, 
        /// but both parameters can be initialized with their respective default values.
        /// </summary>
        /// <param name="mongoDbConnectionString"></param>
        /// <param name="databaseName"></param>
        /// <param name="logLevel"></param>
        /// <param name="additionalDispatchCreatorStrategies"></param>
        /// <param name="bulkVendorName"></param>
        /// <param name="bulkReadSize"></param>
        /// <param name="surveyBaseDomain"></param>
        /// <param name="unsubscribeUrl"></param>
        public DispatchHandler(string mongoDbConnectionString,
            string databaseName,
            int logLevel = 5,
            Dictionary<string, Func<IDispatchVendor>> additionalDispatchCreatorStrategies = default,
            string bulkVendorName = "sparkpost",
            int bulkReadSize = 10000,
            string surveyBaseDomain = "nps.bz",
            string unsubscribeUrl = "https://cx.getcloudcherry.com/l/unsub/?token=")
        {
            Resources.GetOrCreateInstance(mongoDbConnectionString, databaseName, logLevel, additionalDispatchCreatorStrategies,
                bulkVendorName, bulkReadSize, surveyBaseDomain, unsubscribeUrl);
        }

        public async Task ProcessSingleMessage(QueueData queueData)
        {
            MessagePayload messagePayload = new MessagePayload(queueData);
            try
            {
                messagePayload.Validate();
                if (!messagePayload.IsProcessable)
                    return;

                messagePayload.ConfigureChannel();
                if (messagePayload.IsEmailDelivery == null)
                    return;

                await messagePayload.ConfigureUserData();
                if (!messagePayload.IsUserDataLogEventConfigured)
                    return;

                try
                {
                    messagePayload.PrepareForHashLookUps();

                    messagePayload.ConfigureVendor();
                    if (!messagePayload.IsVendorConfigured)
                        return;

                    messagePayload.ConfigureVendorFlag();
                    if (messagePayload.Vendor.IsBulkVendor)
                    {
                        await Utils.InsertBulkMessagePayload(messagePayload);
                        return;
                    }

                    SingleDispatch singleDispatch = new SingleDispatch();
                    singleDispatch = new SingleDispatch { MessagePayload = messagePayload };
                    singleDispatch.ConfigureDispatchVendor();
                    if (!singleDispatch.IsDispatcConfigured)
                        return;
                    await singleDispatch.DispatchReadyVendor.RunAsync(singleDispatch.MessagePayload);
                }
                catch (Exception ex)
                {
                    messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.InternalException(ex)));
                    messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful,
                        messagePayload.IsEmailDelivery.Value ? EventChannel.Email : EventChannel.SMS, messagePayload.QueueData, IRDLM.InternalException(ex)));
                }
            }
            catch (Exception ex)
            {
                messagePayload.LogEvents.Add(Utils.CreateLogEvent(queueData, IRDLM.InternalException(ex)));
            }
            finally
            {
                await Utils.FlushLogs(new List<MessagePayload> { messagePayload });
            }
        }

        public async Task ProcessMultipleMessage(bool isLate)
        {
            List<LogEvent> logEvents = new List<LogEvent>();
            if (isLate)
                logEvents.Add(Utils.CreateLogEvent(null, IRDLM.TimeTriggerRunningLate));
            List<MessagePayload> messagePayloads = new List<MessagePayload>();
            List<DB_MessagePayload> dB_MessagePayloads = await Utils.ReadBulkMessagePayloads();
            if (dB_MessagePayloads.Count > 0)
            {
                logEvents.Add(Utils.CreateLogEvent(null, IRDLM.TimeTriggerStart));
                try
                {
                    await Utils.UpdateBulkMessagePayloads(dB_MessagePayloads);
                    Dictionary<string, List<MessagePayload>> ListOfMessagePayloadsByTemplateId = new Dictionary<string, List<MessagePayload>>();
                    var dB_MessagePayloadsPerVendor = dB_MessagePayloads.GroupBy(x => x.BulkVendorName);
                    foreach (var payloads in dB_MessagePayloadsPerVendor)
                    {
                        foreach (DB_MessagePayload dB_MessagePayload in payloads)
                        {
                            MessagePayload messagePayload = JsonConvert.DeserializeObject<MessagePayload>(dB_MessagePayload.MessagePayload);
                            messagePayloads.Add(messagePayload);
                            messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.ReadFromDB(Resources.GetInstance().BulkVendorName)));
                            if (!ListOfMessagePayloadsByTemplateId.ContainsKey(messagePayload.QueueData.TemplateId))
                                ListOfMessagePayloadsByTemplateId.Add(messagePayload.QueueData.TemplateId, new List<MessagePayload>());
                            ListOfMessagePayloadsByTemplateId[messagePayload.QueueData.TemplateId].Add(messagePayload);
                        }
                        foreach (KeyValuePair<string, List<MessagePayload>> messagePayloadsByTemplateId in ListOfMessagePayloadsByTemplateId)
                        {
                            BulkDispatch bulkDispatch = null;
                            try
                            {
                                bulkDispatch = new BulkDispatch { MessagePayloads = messagePayloadsByTemplateId.Value };
                                bulkDispatch.ConfigureDispatchVendor();
                                if (!bulkDispatch.IsDispatchConfigured)
                                    continue;
                                await bulkDispatch.DispatchReadyVendor.RunAsync(bulkDispatch.MessagePayloads);
                            }
                            catch (Exception ex)
                            {
                                bulkDispatch.MessagePayloads.ForEach(x => x.LogEvents.Add(Utils.CreateLogEvent(x.QueueData, IRDLM.InternalException(ex))));
                                bulkDispatch.MessagePayloads.ForEach(x => x.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful,
                                    x.IsEmailDelivery.Value ? EventChannel.Email : EventChannel.SMS, x.QueueData, IRDLM.InternalException(ex))));
                            }
                        }
                    }
                    
                }
                catch (Exception ex)
                {
                    logEvents.Add(Utils.CreateLogEvent(null, IRDLM.InternalException(ex)));
                }
                finally
                {
                    await Utils.DeleteBulkMessagePayloads(dB_MessagePayloads);
                    await Utils.FlushLogs(messagePayloads);
                }
                logEvents.Add(Utils.CreateLogEvent(null, IRDLM.TimeTriggerEnd(dB_MessagePayloads.Count)));
            }
            await Utils.FlushLogs(logEvents);
        }
    }
}
