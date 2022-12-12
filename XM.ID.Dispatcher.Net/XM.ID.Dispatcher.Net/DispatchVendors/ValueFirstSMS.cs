using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Dispatcher.Net.DispatchVendors
{
    class ValueFirstSMS : IBulkDispatchVendor
    {
        public Vendor Vendor { get; set; }

        private string BearerToken { get; set; }

        public void Setup(Vendor vendor)
        {
            Vendor = vendor;
        }

        public async Task RunAsync(List<MessagePayload> messagePayloads)
        {
            int batchSize = int.Parse(Vendor.VendorDetails["batchSize"]);
            string apiEndpoint = !string.IsNullOrEmpty(Vendor.VendorDetails["EndPoint"]) && Vendor.VendorDetails["EndPoint"].EndsWith("/") ?
                Vendor.VendorDetails["EndPoint"].Remove(Vendor.VendorDetails["EndPoint"].Length -1) : Vendor.VendorDetails["EndPoint"];
            List<List<MessagePayload>> batchesOfMessagePayload = new List<List<MessagePayload>>();
            // Dividing in batch sizes.
            int noOfBatches = messagePayloads.Count / batchSize;
            if (messagePayloads.Count % batchSize > 0)
                noOfBatches++;
            for (int i = 0; i < noOfBatches; i++)
                batchesOfMessagePayload.Add(messagePayloads.Skip(i * batchSize).Take(batchSize).ToList());

            // Calling API to get bearer token
            var userId = Vendor.VendorDetails["UserId"];
            var password = Vendor.VendorDetails["Password"];
            var basicAuthToken = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(userId + ":" + password));
            HttpRequestMessage tokenRequest = new HttpRequestMessage(HttpMethod.Post, Vendor.VendorDetails["EndPoint"] + 
                "/api/messages/token?action=generate");
            tokenRequest.Headers.Add("Authorization", "Basic " + basicAuthToken);
            HttpResponseMessage tokenResponse = await Resources.GetInstance().HttpClient.SendAsync(tokenRequest);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                foreach (MessagePayload messagePayload in messagePayloads)
                {
                    HttpRequestException httpRequestException = new HttpRequestException($"Value First Token Generate " +
                        $"Post API didn't return a 2xx => response headers: " +
                        $"{JsonConvert.SerializeObject(tokenResponse)} => response content: {await tokenResponse.Content.ReadAsStringAsync()}");
                    messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                    messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.SMS,
                        messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                }
            }
            else
            {
                BearerToken = JsonConvert.DeserializeObject<Token>(await tokenResponse.Content.ReadAsStringAsync()).token;
                foreach (List<MessagePayload> batchOfMessagePayload in batchesOfMessagePayload)
                {
                    try
                    {
                        // Creating request to send SMS.
                        HttpRequestMessage sendSMSRequest = new HttpRequestMessage(HttpMethod.Post, Vendor.VendorDetails["EndPoint"] + 
                            "/servlet/psms.JsonEservice");
                        sendSMSRequest.Headers.Add("Authorization", "Bearer " + BearerToken);
                        VFSMS vFSMSBody = new VFSMS
                        {
                            VER = "1.2",
                            USER = new USER
                            {
                                demohansxml = string.Empty
                            },
                            DLR = new DLR
                            {
                                URL = string.Empty
                            },
                            SMS = new List<SMS>()
                        };
                        foreach (MessagePayload messagePayload in batchOfMessagePayload)
                        {
                            Utils.PerformLookUps(messagePayload.QueueData);
                            SMS smsBody = new SMS
                            {
                                UDH = "0",
                                ID = messagePayload.QueueData.MobileNumber,
                                PROPERTY = "0",
                                CODING = "1",
                                TEXT = messagePayload.QueueData.TextBody,
                                ADDRESS = new List<ADDRESS>
                            {
                                new ADDRESS
                                {
                                    FROM = Vendor.VendorDetails["SenderId"],
                                    SEQ = messagePayload.QueueData.MobileNumber,
                                    TAG = "Invitation Delivery Dispatcher",
                                    TO = messagePayload.QueueData.MobileNumber
                                }
                            }
                            };
                            vFSMSBody.SMS.Add(smsBody);
                        }
                        var jsonbody = JsonConvert.SerializeObject(vFSMSBody);
                        sendSMSRequest.Content = new StringContent(jsonbody, Encoding.UTF8, "application/json");
                        sendSMSRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                        HttpResponseMessage response = await Resources.GetInstance().HttpClient.SendAsync(sendSMSRequest);
                        if (!response.IsSuccessStatusCode)
                        {
                            var responseString = await response.Content.ReadAsStringAsync();
                            HttpRequestException httpRequestException = new HttpRequestException($"VFSMS Post API didn't return a 2xx => response headers: " +
                                    $"{JsonConvert.SerializeObject(response)} => response content: {responseString}");
                            foreach (MessagePayload messagePayload in batchOfMessagePayload)
                            {
                                messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                                messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.SMS,
                                    messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                            }
                        }
                        else
                        {
                            SingleResponse singleResponse = default;
                            MultipleResponse multipleResponse = default;
                            string content = await response.Content.ReadAsStringAsync();
                            try
                            {
                                singleResponse = JsonConvert.DeserializeObject<SingleResponse>(content);
                            }
                            catch
                            {
                                try
                                {
                                    multipleResponse = JsonConvert.DeserializeObject<MultipleResponse>(content);
                                }
                                catch
                                {
                                    multipleResponse = default;
                                }
                            }

                            if (string.IsNullOrEmpty(content) || (singleResponse != default && singleResponse.MESSAGEACK.GUID == null) 
                                || (singleResponse != default && (singleResponse.MESSAGEACK.GUID.Err != null 
                                || singleResponse.MESSAGEACK.GUID.ERROR != null)))
                            {
                                // API failed case.
                                HttpRequestException httpRequestException = new HttpRequestException($"VFSMS failed => response content: " +
                                            $"{content}");
                                foreach (MessagePayload messagePayload in batchOfMessagePayload)
                                {
                                    
                                    messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                                    messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.SMS,
                                           messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                                }
                            }
                            else if (singleResponse != default && multipleResponse == default)
                            {
                                foreach (MessagePayload messagePayload in batchOfMessagePayload)
                                {
                                    messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchSuccessful(Vendor.VendorName)));
                                    messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchSuccessful, EventChannel.SMS,
                                        messagePayload.QueueData, IRDLM.DispatchSuccessful(Vendor.VendorName)));
                                }
                            }
                            else
                            {
                                foreach (MessagePayload messagePayload in batchOfMessagePayload)
                                {
                                    var recordStatus = multipleResponse?.MESSAGEACK?.GUID.Find(x => x.ID?.ToString() == messagePayload.QueueData.MobileNumber);
                                    if (recordStatus == null)
                                    {
                                        HttpRequestException httpRequestException = new HttpRequestException($"VFSMS failed => response content: " +
                                            $"{content}");
                                        messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                                        messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.SMS,
                                            messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                                    }
                                    else if (recordStatus.Err != null || recordStatus.ERROR != null)
                                    {
                                        HttpRequestException httpRequestException = new HttpRequestException($"VFSMS failed => response content: " +
                                            $"{JsonConvert.SerializeObject(recordStatus)}");
                                        messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                                        messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.SMS,
                                            messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                                    }
                                    else
                                    {
                                        messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchSuccessful(Vendor.VendorName)));
                                        messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchSuccessful, EventChannel.SMS,
                                            messagePayload.QueueData, IRDLM.DispatchSuccessful(Vendor.VendorName)));
                                    }
                                }
                            }
                            // API successfully send SMS
                        }
                    }
                    catch (Exception ex)
                    {
                        foreach (MessagePayload messagePayload in batchOfMessagePayload)
                        {
                            messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, ex)));
                            messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.SMS,
                                   messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, ex)));
                        }
                    }
                }
            }
        }

        #region Token Model
        public class Token
        {
            public string token { get; set; }
            public string expiryDate { get; set; }
        }
        #endregion

        #region VF SMS Models
        public class USER
        {
            public string demohansxml { get; set; }
        }

        public class DLR
        {
            [JsonProperty("@URL")]
            public string URL { get; set; }
        }

        public class ADDRESS
        {
            [JsonProperty("@FROM")]
            public string FROM { get; set; }
            [JsonProperty("@TO")]
            public string TO { get; set; }
            [JsonProperty("@SEQ")]
            public string SEQ { get; set; }
            [JsonProperty("@TAG")]
            public string TAG { get; set; }
        }

        public class SMS
        {
            [JsonProperty("@UDH")]
            public string UDH { get; set; }
            [JsonProperty("@CODING")]
            public string CODING { get; set; }
            [JsonProperty("@TEXT")]
            public string TEXT { get; set; }
            [JsonProperty("@PROPERTY")]
            public string PROPERTY { get; set; }
            [JsonProperty("@ID")]
            public string ID { get; set; }
            public List<ADDRESS> ADDRESS { get; set; }
        }

        public class VFSMS
        {
            [JsonProperty("@VER")]
            public string VER { get; set; }
            public USER USER { get; set; }
            public DLR DLR { get; set; }
            public List<SMS> SMS { get; set; }
        }
        #endregion

        #region VF SMS Response Model
        public class ERROR
        {
            public int CODE { get; set; }
            public string SEQ { get; set; }
        }

        public class Err
        {
            public string Desc { get; set; }
            public int Code { get; set; }
        }

        public class GUIDClass
        {
            public string SUBMITDATE { get; set; }
            public string GUID { get; set; }
            public Err Err { get; set; }
            public ERROR ERROR { get; set; }
            public string ID { get; set; }
        }

        public class MESSAGEACK
        {
            public GUIDClass GUID { get; set; }
        }

        public class MESSAGEACKMultiple
        {
            public List<GUIDClass> GUID { get; set; }
        }

        public class SingleResponse
        {
            public MESSAGEACK MESSAGEACK { get; set; }
        }

        public class MultipleResponse
        {
            public MESSAGEACKMultiple MESSAGEACK { get; set; }
        }
        #endregion
    }
}
