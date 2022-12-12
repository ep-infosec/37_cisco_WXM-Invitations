using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace InvitationNotification
{
    class SendOutNotification
    {
        SMTPServer customMailSever;
        public SendOutNotification(SMTPServer smtp)
        {
            customMailSever = smtp;
        }

        public async Task SendOutEmails(List<MailMessage> mailMessages)
        {

            var mailServer = GetCustomSMTPClient();
            if (mailServer == null)
                return;
            foreach (var mail in mailMessages)
            {
                try
                {
                    await mailServer.SendMailAsync(mail);

                }
                catch (Exception ex0)
                {

                }
            }
        }

        SmtpClient GetCustomSMTPClient()
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
