using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvitationNotification
{
    #region Notification Configs
    public class Configuration
    {
        public string MongoConnectionStriong = "";

    }
    public class EmailTemplateViewModel
    {

    }
    public class SMTPServer
    {
        /// <summary>
        /// ex: Your Company Name
        /// </summary>

        public string FromName { get; set; }

        /// <summary>
        /// ex: address@yourserver.net
        /// </summary>

        public string FromAddress { get; set; }

        /// <summary>
        /// ex: smtp.yoursever.net
        /// </summary>
        public string Server { get; set; }

        /// <summary>
        /// Usually address@yourserver.net
        /// </summary>
        public string Login { get; set; }

        /// <summary>
        /// SecretKey to send email
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Ex: 587(Submission), 25(Classic SMTP)
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Set to require Secure SSL Connection
        /// </summary>
        public bool EnableSSL { get; set; }

        public override string ToString()
        {
            string text = Server + ":" + Port + " SSL:" + EnableSSL + " Login:" + Login + " From:" + FromAddress;
            return text;
        }
    }
    public class Frequency
    {
        public string Every { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public int RealtImeMaxLevel { get; set; }

    }

    public class ProjectedLog
    {
        public DateTime Created { get; set; }
        public string BatchId { get; set; }
        public string DispatchId { get; set; }
        public string LogLevel { get; set; }
        public string Message { get; set; }
    }
    #endregion
}
