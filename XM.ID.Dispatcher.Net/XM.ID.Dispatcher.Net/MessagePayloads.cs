using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Dispatcher.Net
{
    public class MessagePayload
    {
        [JsonIgnore]
        internal bool IsProcessable { get; set; }
        [JsonIgnore]
        internal bool? IsEmailDelivery { get; set; }
        [JsonIgnore]
        internal bool IsUserDataLogEventConfigured { get; set; }
        [JsonIgnore]
        internal bool IsVendorConfigured { get; set; }
        [JsonIgnore]
        internal bool IsBulkVendor { get; set; }
        /// <summary>
        /// Message received from Queue
        /// </summary>
        public QueueData QueueData { get; set; }
        /// <summary>
        /// User-Data-Log-Event
        /// </summary>
        public LogEvent Invitation { get; set; }
        /// <summary>
        /// Invitation's Vendor-Details
        /// </summary>
        public Vendor Vendor { get; set; }
        /// <summary>
        /// Logs to capture events at an Application level
        /// </summary>
        [JsonIgnore]
        public List<LogEvent> LogEvents { get; set; } = new List<LogEvent>();
        /// <summary>
        /// Logs to capture events at an Invitation level
        /// </summary>
        [JsonIgnore]
        public List<InvitationLogEvent> InvitationLogEvents { get; set; } = new List<InvitationLogEvent>();

        [JsonConstructor]
        public MessagePayload()
        {

        }

        public MessagePayload(QueueData queueData)
        {
            QueueData = queueData;
            LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.Dequeued));
        }

        internal void Validate()
        {
            bool isTokenIdPresent = !string.IsNullOrWhiteSpace(QueueData.TokenId);
            bool isBatchIdPresent = !string.IsNullOrWhiteSpace(QueueData.BatchId);
            bool isDispatchIdPresent = !string.IsNullOrWhiteSpace(QueueData.DispatchId);
            if (isTokenIdPresent && isBatchIdPresent && isDispatchIdPresent)
            {
                IsProcessable = true;
                LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.Validated(QueueData.AdditionalURLParameter)));
            }
            else
            {
                IsProcessable = false;
                LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.Invalidated));
            }
        }

        internal void ConfigureChannel()
        {
            if (!string.IsNullOrWhiteSpace(QueueData.EmailId) && !string.IsNullOrWhiteSpace(QueueData.MobileNumber))
            {
                IsEmailDelivery = null;
                LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.ChannelNotConfigured1));
            }
            else if (!string.IsNullOrWhiteSpace(QueueData.EmailId))
            {
                IsEmailDelivery = true;
                LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.EmailChannelConfigured));
            }
            else if (!string.IsNullOrEmpty(QueueData.MobileNumber))
            {
                IsEmailDelivery = false;
                LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.SmsChannelConfigured));
            }
            else
            {
                IsEmailDelivery = null;
                LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.ChannelNotConfigured2));
            }
        }

        internal async Task ConfigureUserData()
        {
            // Update this to get record by PartnerDBDocumentId.
            if (string.IsNullOrEmpty(QueueData.PartnerDBDocumentId))
                Invitation = await Resources.GetInstance().LogEventCollection.Find(x => x.TokenId == QueueData.TokenId &&
            x.BatchId == QueueData.BatchId && x.DispatchId == QueueData.DispatchId).FirstOrDefaultAsync();
            else
                Invitation = await Resources.GetInstance().LogEventCollection.Find(x => x.Id == QueueData.PartnerDBDocumentId).FirstOrDefaultAsync();
            if (Invitation == default)
            {
                IsUserDataLogEventConfigured = false;
                LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.UserDataNotFound));
            }
            else
            {
                IsUserDataLogEventConfigured = true;
                LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.UserDataFound(Invitation.Id)));
            }
        }

        internal void PrepareForHashLookUps()
        {
            Dictionary<string, string> hashLookUpDict = new Dictionary<string, string>();
            foreach (Prefill prefill in Invitation.Prefills)
            {
                if (prefill.Input_Hash != null)
                {
                    if (!hashLookUpDict.ContainsKey(prefill.Input_Hash))
                        hashLookUpDict.Add(prefill.Input_Hash, prefill.Input);
                }
            }

            QueueData.CommonIdentifier = Invitation.Target;
            if (IsEmailDelivery.Value)
            {
                if (hashLookUpDict.TryGetValue(QueueData.EmailId, out string emailId))
                    QueueData.EmailId = emailId;
            }
            else
            {
                if (hashLookUpDict.TryGetValue(QueueData.MobileNumber, out string mobileNumber))
                    QueueData.MobileNumber = mobileNumber;
            }

            Dictionary<string, string> unhashedMappedValues = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> keyValuePair in QueueData.MappedValue)
            {
                if (!string.IsNullOrEmpty(keyValuePair.Value))
                {
                    if (hashLookUpDict.TryGetValue(keyValuePair.Value, out string unhashedValue))
                        unhashedMappedValues.Add(keyValuePair.Key, unhashedValue);
                    else
                        unhashedMappedValues.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }
            QueueData.MappedValue = unhashedMappedValues;
            LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.HashLookUpDictConfigured));
        }

        internal void ConfigureVendor()
        {
            DispatchChannel dispatchChannel = Resources.GetInstance().AccountConfiguration.DispatchChannels?.Find(x => x.DispatchId == QueueData.DispatchId);
            if (dispatchChannel == default)
            {
                IsVendorConfigured = false;
                LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.DispatchChannelNotFound));
                InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful,
                    IsEmailDelivery.Value ? EventChannel.Email : EventChannel.SMS, QueueData, IRDLM.DispatchChannelNotFound));
            }
            else
            {
                string vendorName = null;
                if (IsEmailDelivery.Value)
                    vendorName = dispatchChannel?.ChannelDetails?.Email?.IsValid ?? false ? dispatchChannel.ChannelDetails.Email.Vendorname : null;
                else
                    vendorName = dispatchChannel?.ChannelDetails?.Sms?.IsValid ?? false ? dispatchChannel.ChannelDetails.Sms.Vendorname : null;
                if (vendorName == null)
                {
                    IsVendorConfigured = false;
                    LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.DispatchVendorNameMissing));
                    InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful,
                        IsEmailDelivery.Value ? EventChannel.Email : EventChannel.SMS, QueueData, IRDLM.DispatchVendorNameMissing));
                }
                else
                {
                    LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.DispatchVendorNamePresent(vendorName)));
                    Vendor = Resources.GetInstance().AccountConfiguration.Vendors?.Find(x => string.Equals(x.VendorName, vendorName, StringComparison.InvariantCultureIgnoreCase));
                    if (Vendor == null)
                    {
                        IsVendorConfigured = false;
                        LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.DispatchVendorConfigMissing));
                        InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful,
                            IsEmailDelivery.Value ? EventChannel.Email : EventChannel.SMS, QueueData, IRDLM.DispatchVendorConfigMissing));
                    }
                    else
                    {
                        IsVendorConfigured = true;
                        LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.DispatchVendorConfigPresent(Vendor)));
                    }
                }
            }
        }

        internal void ConfigureVendorFlag()
        {
            IsBulkVendor = Vendor.IsBulkVendor;
            if (IsBulkVendor)
                LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.VendorIsBulk));
            else
                LogEvents.Add(Utils.CreateLogEvent(QueueData, IRDLM.VendorIsNotBulk));
        }
    }

    [BsonIgnoreExtraElements]
    internal class DB_MessagePayload
    {
        [BsonId]
        public string Id { get; set; }
        public string BulkVendorName { get; set; }
        public string Status { get; set; }
        public DateTime InsertTime { get; set; }
        public string MessagePayload { get; set; }

        public DB_MessagePayload(MessagePayload messagePayload)
        {
            Id = ObjectId.GenerateNewId().ToString();
            MessagePayload = JsonConvert.SerializeObject(messagePayload);
            Status = "Ready";
            BulkVendorName = messagePayload.Vendor.VendorName.ToLower();
            InsertTime = DateTime.UtcNow;
        }
    }
}
