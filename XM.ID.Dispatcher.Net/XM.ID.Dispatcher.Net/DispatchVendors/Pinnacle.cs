using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Dispatcher.Net.DispatchVendors
{
    class Pinnacle : IBulkDispatchVendor
    {
        public Vendor Vendor { get; set; }

        public void Setup(Vendor vendor)
        {
            Vendor = vendor;
        }

        public async Task RunAsync(List<MessagePayload> messagePayloads)
        {
            int batchSize = int.Parse(Vendor.VendorDetails["batchSize"]);
            List<List<MessagePayload>> batchesOfMessagePayload = new List<List<MessagePayload>>();
            int noOfBatches = messagePayloads.Count / batchSize;
            if (messagePayloads.Count % batchSize > 0)
                noOfBatches++;
            for (int i = 0; i < noOfBatches; i++)
                batchesOfMessagePayload.Add(messagePayloads.Skip(i * batchSize).Take(batchSize).ToList());

            foreach (List<MessagePayload> batchOfMessagePayload in batchesOfMessagePayload)
            {
                try
                {
                    MSGData mSGData = new MSGData
                    {
                        data = new List<Data>()
                    };
                    foreach (MessagePayload messagePayload in batchOfMessagePayload)
                    {
                        Utils.PerformLookUps(messagePayload.QueueData);
                        Data record = new Data
                        {
                            message = messagePayload.QueueData.TextBody,
                            mobile = messagePayload.QueueData.MobileNumber
                        };
                        mSGData.data.Add(record);
                    }
                    var formContent = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("senderid", Vendor.VendorDetails["SenderId"]),
                            new KeyValuePair<string, string>("apikey", Vendor.VendorDetails["ApiKey"]),
                            new KeyValuePair<string, string>("mtype", "TXT"),
                            new KeyValuePair<string, string>("subdatatype", "M"),
                            new KeyValuePair<string, string>("response", "Y"),
                            new KeyValuePair<string, string>("msgdata", JsonConvert.SerializeObject(mSGData))
                        });
                    HttpResponseMessage response = await Resources.GetInstance().HttpClient.PostAsync(new Uri(Vendor.VendorDetails["EndPoint"]), formContent);
                    string responseAsString = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseAsString);
                    // Status code is not 200
                    if (!response.IsSuccessStatusCode)
                    {
                        foreach (MessagePayload messagePayload in batchOfMessagePayload)
                        {
                            HttpRequestException httpRequestException = new HttpRequestException($"Pinnacle Post API didn't return a 2xx => response headers: " +
                                $"{JsonConvert.SerializeObject(response)} => response content: {await response.Content.ReadAsStringAsync()}");
                            messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                            messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.SMS,
                                messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                        }
                    }
                    else
                    {
                        var result = responseObject.TryGetValue("result", out object resultString);
                        // Log for failure in case API returned 200 but failed to send SMS.
                        if (!result || string.IsNullOrEmpty(resultString.ToString()) || resultString.ToString() == "False")
                        {
                            foreach (MessagePayload messagePayload in batchOfMessagePayload)
                            {
                                HttpRequestException httpRequestException = new HttpRequestException($"Pinnacle Post API returned a 2xx => response headers: " +
                                    $"{JsonConvert.SerializeObject(response)} => response content: {await response.Content.ReadAsStringAsync()}");
                                messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                                messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.SMS,
                                    messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                            }
                        }
                        else
                        {
                            // API successfully send SMS
                            foreach (MessagePayload messagePayload in batchOfMessagePayload)
                            {
                                messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchSuccessful(Vendor.VendorName)));
                                messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchSuccessful, EventChannel.SMS,
                                    messagePayload.QueueData, IRDLM.DispatchSuccessful(Vendor.VendorName)));
                            }
                        }
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

        /// <summary>
        /// Mobile and message data.
        /// </summary>
        public class Data
        {
            public string mobile { get; set; }
            public string message { get; set; }
        }

        /// <summary>
        /// Pinnacle message data class.
        /// </summary>
        public class MSGData
        {
            public List<Data> data { get; set; }
        }
    }
}
