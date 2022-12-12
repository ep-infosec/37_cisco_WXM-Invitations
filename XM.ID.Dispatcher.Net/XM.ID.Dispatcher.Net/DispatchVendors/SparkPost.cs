using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Dispatcher.Net.DispatchVendors
{
    internal class SparkPost : IBulkDispatchVendor
    {
        public Vendor Vendor { get; set; }

        private StringBuilder newSubject = new StringBuilder();
        private StringBuilder newHtmlBody = new StringBuilder();
        private StringBuilder newTextBody = new StringBuilder();
        private Dictionary<string, string> qIdLookUpDict = new Dictionary<string, string>();

        public void Setup(Vendor vendor)
        {
            Vendor = vendor;
        }

        public async Task RunAsync(List<MessagePayload> messagePayloads)
        {
            var subject = messagePayloads.ElementAt(0).QueueData.Subject;
            var htmlBody = messagePayloads.ElementAt(0).QueueData.HTMLBody;
            var textBody = messagePayloads.ElementAt(0).QueueData.TextBody;
            Prepare(subject, htmlBody, textBody);

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
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Vendor.VendorDetails["url"]);
                    request.Headers.Add("Authorization", Vendor.VendorDetails["apiKey"]);
                    SparkPostRequest sparkPostRequest = new SparkPostRequest
                    {
                        content = new content
                        {
                            from = new from
                            {
                                email = Vendor.VendorDetails["senderEmail"],
                                name = Vendor.VendorDetails["senderName"]
                            },
                            html = newHtmlBody.ToString(),
                            subject = newSubject.ToString(),
                            text = newTextBody.ToString()
                        }
                    };
                    sparkPostRequest.recipients = new List<recipient>();
                    foreach (MessagePayload messagePayload in batchOfMessagePayload)
                    {
                        Dictionary<string, string> substitutionDataDict = new Dictionary<string, string>();
                        foreach (KeyValuePair<string, string> kvp in qIdLookUpDict)
                        {
                            if (messagePayload.QueueData.MappedValue.ContainsKey(kvp.Key))
                                substitutionDataDict.Add(kvp.Value, messagePayload.QueueData.MappedValue[kvp.Key]);
                        }
                        substitutionDataDict.Add("Token", messagePayload.QueueData.TokenId);
                        substitutionDataDict.Add("SurveyURL", Utils.GetSurveyURL(messagePayload.QueueData));
                        substitutionDataDict.Add("UnsubscribeURL", Utils.GetUnsubscribeURL(messagePayload.QueueData));

                        recipient recipient = new recipient
                        {
                            address = new address { email = messagePayload.QueueData.EmailId },
                            substitution_data = substitutionDataDict
                        };
                        sparkPostRequest.recipients.Add(recipient);
                    };
                    string jsonbody = JsonConvert.SerializeObject(sparkPostRequest);
                    request.Content = new StringContent(jsonbody, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await Resources.GetInstance().HttpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        foreach (MessagePayload messagePayload in batchOfMessagePayload)
                        {
                            HttpRequestException httpRequestException = new HttpRequestException($"Spark Post API didn't return a 2xx => response headers: " +
                                $"{JsonConvert.SerializeObject(response)} => response content: {await response.Content.ReadAsStringAsync()}");
                            messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                            messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.Email,
                                messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                        }
                    }
                    else
                    {
                        foreach (MessagePayload messagePayload in batchOfMessagePayload)
                        {
                            messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchSuccessful(Vendor.VendorName)));
                            messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchSuccessful, EventChannel.Email,
                                messagePayload.QueueData, IRDLM.DispatchSuccessful(Vendor.VendorName)));
                        }
                    }
                }
                catch (Exception ex)
                {
                    foreach (MessagePayload messagePayload in batchOfMessagePayload)
                    {
                        messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, ex)));
                        messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.Email,
                               messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, ex)));
                    }
                }
            }
        }

        public void Prepare(string subject, string htmlBody, string textBody)
        {
            int count = 0;
            string matchString = @"(?<=\{\{)\s*([a-f\d]{24})(\s+or\s+'.*?'\s*)?(?=\}\})";
            RegexOptions options = RegexOptions.Multiline | RegexOptions.IgnoreCase;
            //Check Subject
            if (!string.IsNullOrWhiteSpace(subject))
            {
                newSubject = new StringBuilder(subject);
                foreach (Match m in Regex.Matches(subject, matchString, options))
                {
                    string qId = m.Groups[1].Value;
                    string replacement = null;
                    if (!qIdLookUpDict.ContainsKey(qId))
                    {
                        replacement = $"substitution{count}";
                        qIdLookUpDict.Add(qId, replacement);
                        count++;
                    }
                    else
                        replacement = qIdLookUpDict[qId];
                    newSubject.Replace(qId, replacement);
                }
            }
            //Check HtmlBody
            if (!string.IsNullOrWhiteSpace(htmlBody))
            {
                newHtmlBody = new StringBuilder(htmlBody);
                foreach (Match m in Regex.Matches(htmlBody, matchString, options))
                {
                    string qId = m.Groups[1].Value;
                    string replacement = null;
                    if (!qIdLookUpDict.ContainsKey(qId))
                    {
                        replacement = $"substitution{count}";
                        qIdLookUpDict.Add(qId, replacement);
                        count++;
                    }
                    else
                        replacement = qIdLookUpDict[qId];
                    newHtmlBody.Replace(qId, replacement);
                }
            }
            //Check TextBody
            if (!string.IsNullOrWhiteSpace(textBody))
            {
                newTextBody = new StringBuilder(textBody);
                foreach (Match m in Regex.Matches(textBody, matchString, options))
                {
                    string qId = m.Groups[1].Value;
                    string replacement = null;
                    if (!qIdLookUpDict.ContainsKey(qId))
                    {
                        replacement = $"substitution{count}";
                        qIdLookUpDict.Add(qId, replacement);
                        count++;
                    }
                    else
                        replacement = qIdLookUpDict[qId];
                    newTextBody.Replace(qId, replacement);
                }
            }
        }

        //Required: Lowercase
        private class from
        {
            public string name { get; set; }
            public string email { get; set; }
        }

        //Required: Lowercase
        private class content
        {
            public from from { get; set; }
            public string subject { get; set; }
            public string html { get; set; }
            public string text { get; set; }
        }

        //Required: Lowercase
        private class address
        {
            public string email { get; set; }
        }

        //Required: Lowercase
        private class recipient
        {
            public address address { get; set; }
            public Dictionary<string, string> substitution_data { get; set; }
        }

        //Required: Lowercase
        private class SparkPostRequest
        {
            public List<recipient> recipients { get; set; }
            public content content { get; set; }
        }
    }
}
