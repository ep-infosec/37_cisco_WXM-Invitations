using DispatcherEmailService.Cache;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Mail;
using XM.ID.Net;

namespace DispatcherEmailService.Helper
{
    public class CustomSMTP
    {
        private Vendor _vendor { get; set; }
        private readonly ConfigurationCache _configurationCache;
        public object SmtpLock { get; private set; }
        public CustomSMTP(ConfigurationCache configurationCache)
        {
            SmtpLock = new object();
            _configurationCache = configurationCache;
        }

        public void SendEmail(RequestBody requestBody)
        {
            try
            {
                string confString = _configurationCache.GetConfigurationDataFromCache();
                if (string.IsNullOrEmpty(confString))
                    throw new Exception("Account Configuration not found.");

                AccountConfiguration accountConfiguration = JsonConvert.DeserializeObject<AccountConfiguration>(confString);
                _vendor = accountConfiguration.Vendors?.Find(vendor => vendor.VendorName?.ToLower() == "customsmtp");

                if (_vendor == null)
                    throw new Exception("Custom SMTP vendor details not found");

                MailAddressCollection collection = new MailAddressCollection();
                MailMessage mail = new MailMessage
                {
                    From = new MailAddress(_vendor.VendorDetails["SenderAddress"], _vendor.VendorDetails["SenderName"])
                };
                mail.To.Add(requestBody.EmailId);
                SmtpClient client = new SmtpClient
                {
                    Timeout = 25 * 1000,
                    Port = int.Parse(_vendor.VendorDetails["Port"]),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Credentials = new NetworkCredential(_vendor.VendorDetails["SmtpUsername"], _vendor.VendorDetails["SmtpPassword"]),
                    Host = _vendor.VendorDetails["SmtpServer"],
                    EnableSsl = Boolean.Parse(_vendor.VendorDetails["SSL"])
                };
                mail.Subject = requestBody.Subject;
                if (string.IsNullOrEmpty(requestBody.TextBody))
                {
                    mail.Body = requestBody.HTMLBody;
                    mail.IsBodyHtml = true;
                }
                else
                    mail.Body = requestBody.TextBody;
                client.Send(mail);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
