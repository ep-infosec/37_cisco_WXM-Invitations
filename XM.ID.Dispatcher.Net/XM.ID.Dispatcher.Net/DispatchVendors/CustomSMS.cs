using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Dispatcher.Net.DispatchVendors
{
    internal class CustomSMS : ISingleDispatchVendor
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
                string customSmsURL = Vendor.VendorDetails["url"];
                string textBody;
                if (customSmsURL.Contains("YESBNK"))
                    textBody = Utils.EncodeTextBody(messagePayload.QueueData.TextBody);
                else
                    textBody = messagePayload.QueueData.TextBody;
                customSmsURL = customSmsURL?.Replace("$Message$", textBody);
                customSmsURL = customSmsURL?.Replace("$To$", messagePayload.QueueData.MobileNumber);
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, customSmsURL);
                HttpResponseMessage response = await Resources.GetInstance().HttpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    HttpRequestException httpRequestException = new HttpRequestException($"Custom SMS API didn't return a 2xx => " +
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
    }
}
