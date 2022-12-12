using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using XM.ID.Net;

namespace DPReporting
{
    class SendOutReport
    {
        SMTPServer customMailSever;
        ApplicationLog log;

        public SendOutReport(SMTPServer smtp, ApplicationLog applog)
        {
            customMailSever = smtp;
            applog = log;
        }

        public async Task SendOutEmails(MailMessage mailMessage)
        {

            var mailServer = getCustomSMTPClient();
            if (mailServer == null)
                return;
            try
            {
                mailServer.Send(mailMessage);

            }
            catch (Exception ex0)
            {
                log.logMessage += $"Exception while sending emails- " + ex0.Message;
                log.AddLogsToFile(DateTime.UtcNow);
                return;
            }
        }

        SmtpClient getCustomSMTPClient()
        {
            try
            {
                if (customMailSever == null)
                    return null;

                if (customMailSever.Server.Contains('@')) // Use just the server host
                    customMailSever.Server = customMailSever.Server.Split('@')[1];

                if (customMailSever.Port < 20) // Invalid port work-around
                    customMailSever.Port = 25;

                if (customMailSever.Server == "smtp.gmail.com" || customMailSever.Server == "smtp.office365.com")
                { // Known SSL SMTP
                    customMailSever.EnableSSL = true;
                    customMailSever.Port = 587;
                }

                return new System.Net.Mail.SmtpClient
                {
                    Host = customMailSever.Server,
                    Port = customMailSever.Port,
                    EnableSsl = customMailSever.EnableSSL,
                    UseDefaultCredentials = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Credentials = new System.Net.NetworkCredential(customMailSever.Login, customMailSever.Password)
                };
            }
            catch (Exception ex0)
            {
                return null;
            }

        }
    }
}
