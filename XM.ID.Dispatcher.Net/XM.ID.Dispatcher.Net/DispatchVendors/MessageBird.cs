using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Dispatcher.Net.DispatchVendors
{
    internal class MessageBird : ISingleDispatchVendor
    {
        public Vendor Vendor { get; set; }

        public void Setup(Vendor vendor)
        {
            Vendor = vendor;
        }

        public async Task RunAsync(MessagePayload messagePayload)
        {
            try
            {
                Utils.PerformLookUps(messagePayload.QueueData);
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Vendor.VendorDetails["url"]);
                request.Headers.Add("Authorization", "AccessKey " + Vendor.VendorDetails["accesskey"]);
                MessageBirdRequest messageBirdRequest = new MessageBirdRequest
                {
                    body = messagePayload.QueueData.TextBody,
                    originator = Vendor.VendorDetails["originator"],
                    recipients = messagePayload.QueueData.MobileNumber,
                    shortcode = Vendor.VendorDetails["shortcode"] ?? "",
                    datacoding = Vendor.VendorDetails["datacoding"] ?? "plain"
                };
                string jsonbody = JsonConvert.SerializeObject(messageBirdRequest);
                request.Content = new StringContent(jsonbody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await Resources.GetInstance().HttpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    HttpRequestException httpRequestException = new HttpRequestException($"Message Bird API didn't return a 2xx => " +
                        $"response headers: {JsonConvert.SerializeObject(response)} => response content: {await response.Content.ReadAsStringAsync()}");
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
            catch (Exception ex)
            {
                messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, ex)));
                messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.SMS,
                    messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, ex)));
            }
        }

        //Required: Lowercase
        private class MessageBirdRequest
        {
            public string recipients { get; set; }
            public string originator { get; set; }
            public string body { get; set; }
            public string shortcode { get; set; }
            public string datacoding { get; set; }
        }
    }
}
