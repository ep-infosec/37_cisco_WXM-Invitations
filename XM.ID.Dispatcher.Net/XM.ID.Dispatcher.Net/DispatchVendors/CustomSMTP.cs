using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Dispatcher.Net.DispatchVendors
{
    internal class CustomSMTP : ISingleDispatchVendor
    {
        public Vendor Vendor { get; set; }

        public void Setup(Vendor vendor)
        {
            Vendor = vendor;
        }

        public async Task RunAsync(MessagePayload messagePayload)
        {
            bool.TryParse(Environment.GetEnvironmentVariable("ForwardSMTP"), out bool forwardSMTP);
            if (forwardSMTP)
            {
                try
                {
                    Utils.PerformLookUps(messagePayload.QueueData);
                    HttpRequestMessage sendEmailRequest = new HttpRequestMessage(HttpMethod.Post, Environment.GetEnvironmentVariable("SMTPServiceURL"));
                    string adminUsername = Resources.GetInstance().AccountConfiguration.WXMAdminUser;
                    string adminAPIKeyHashed = GetSHA256Hash(Resources.GetInstance().AccountConfiguration.WXMAPIKey);
                    var basicAuthToken = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(adminUsername + ":" + adminAPIKeyHashed));
                    sendEmailRequest.Headers.Add("Authorization", "Basic " + basicAuthToken);
                    RequestBody requestBody = new RequestBody
                    {
                        EmailId = messagePayload.QueueData.EmailId,
                        HTMLBody = messagePayload.QueueData.HTMLBody,
                        Subject = messagePayload.QueueData.Subject,
                        TextBody = messagePayload.QueueData.TextBody
                    };
                    var jsonbody = JsonConvert.SerializeObject(requestBody);
                    sendEmailRequest.Content = new StringContent(jsonbody, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await Resources.GetInstance().HttpClient.SendAsync(sendEmailRequest);
                    if (response.IsSuccessStatusCode)
                    {
                        messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchSuccessful(Vendor.VendorName)));
                        messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchSuccessful, EventChannel.Email,
                            messagePayload.QueueData, IRDLM.DispatchSuccessful(Vendor.VendorName)));
                    }
                    else
                    {
                        var responseString = await response?.Content?.ReadAsStringAsync();
                        HttpRequestException httpRequestException = new HttpRequestException($"Email Service API failed => status Code: {response?.StatusCode} " +
                            $"=> response content: {responseString}");
                        messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                        messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.Email,
                            messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, httpRequestException)));
                    }
                }
                catch (Exception ex)
                {
                    messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, ex)));
                    messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.Email,
                        messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, ex)));
                }
                finally
                {
                    await Task.CompletedTask;
                }
            }
            else
            {
                try
                {

                    Utils.PerformLookUps(messagePayload.QueueData);
                    MimeMessage mimeMessage = new MimeMessage();
                    BodyBuilder bodyBuilder = new BodyBuilder();
                    mimeMessage.From.Add(new MailboxAddress(Vendor.VendorDetails["senderName"], Vendor.VendorDetails["senderAddress"]));
                    mimeMessage.To.Add(new MailboxAddress(messagePayload.QueueData.EmailId));
                    mimeMessage.Subject = messagePayload.QueueData.Subject;
                    bodyBuilder.TextBody = messagePayload.QueueData.TextBody;
                    bodyBuilder.HtmlBody = messagePayload.QueueData.HTMLBody;
                    mimeMessage.Body = bodyBuilder.ToMessageBody();
                    lock (Resources.GetInstance().SmtpLock)
                    {
                        using SmtpClient smtpClient = CreateSMTPClient();
                        smtpClient.Send(mimeMessage);
                        smtpClient.Disconnect(true);
                    }
                    messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchSuccessful(Vendor.VendorName)));
                    messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchSuccessful, EventChannel.Email,
                        messagePayload.QueueData, IRDLM.DispatchSuccessful(Vendor.VendorName)));
                }
                catch (Exception ex)
                {
                    messagePayload.LogEvents.Add(Utils.CreateLogEvent(messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, ex)));
                    messagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful, EventChannel.Email,
                        messagePayload.QueueData, IRDLM.DispatchUnsuccessful(Vendor.VendorName, ex)));
                }
                finally
                {
                    await Task.CompletedTask;
                }
            }
        }

        private SmtpClient CreateSMTPClient()
        {
            SmtpClient smtpClient = new SmtpClient
            {
                ServerCertificateValidationCallback = (s, c, h, e) => true,
                Timeout = 25 * 1000    //milli-seconds
            };
            if (Boolean.Parse(Vendor.VendorDetails["ssl"]))
                smtpClient.Connect(Vendor.VendorDetails["smtpServer"], Int32.Parse(Vendor.VendorDetails["port"]), SecureSocketOptions.StartTls);
            else
                smtpClient.Connect(Vendor.VendorDetails["smtpServer"], Int32.Parse(Vendor.VendorDetails["port"]), SecureSocketOptions.None);
            smtpClient.Authenticate(Vendor.VendorDetails["smtpUsername"], Vendor.VendorDetails["smtpPassword"]);
            return smtpClient;
        }

        #region SHA256Hashing
        private string GetSHA256Hash(string rawData)
        {
            using SHA256 sha256Hash = SHA256.Create();
            // Compute256Hash - returns byte array  
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return ByteArrayToString(bytes);
        }

        private string ByteArrayToString(byte[] bytes)
        {
            // Convert byte array to a string   
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString().ToLower();
        }
        #endregion
    }
}
