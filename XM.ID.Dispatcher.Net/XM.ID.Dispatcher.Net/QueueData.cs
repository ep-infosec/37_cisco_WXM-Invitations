using System;
using System.Collections.Generic;
using System.Text;

namespace XM.ID.Dispatcher.Net
{
    /// <summary>
    /// Incoming, Delivery-Ready Queue Message from XM
    /// </summary>
    public class QueueData
    {
        /// <summary>
        /// Token Number
        /// </summary>
        public string TokenId { get; set; }
        /// <summary>
        /// Recepient's UUID (Hashed/Actual)
        /// </summary>
        public string CommonIdentifier { get; set; }
        /// <summary>
        /// Recepient's Email-Id
        /// </summary>
        public string EmailId { get; set; }
        /// <summary>
        /// Recepient's Mobile Number
        /// </summary>
        public string MobileNumber { get; set; }
        /// <summary>
        /// Account Name
        /// </summary>
        public string User { get; set; }
        /// <summary>
        /// Email Subject
        /// </summary>
        public string Subject { get; set; }
        /// <summary>
        /// Sms/Email (plain-text) body
        /// </summary>
        public string TextBody { get; set; }
        /// <summary>
        /// Email (rich-html) body
        /// </summary>
        public string HTMLBody { get; set; }
        /// <summary>
        /// Prefilled Question-Answer Values
        /// </summary>
        public Dictionary<string, string> MappedValue { get; set; }
        /// <summary>
        /// Unique Dispatch Identifier
        /// </summary>
        public string DispatchId { get; set; }
        /// <summary>
        /// Unique Batch Identifer
        /// </summary>
        public string BatchId { get; set; }
        /// <summary>
        /// Unique Content-Template Identifier
        /// </summary>
        public string TemplateId { get; set; }
        /// <summary>
        /// Unique Partner DB Document ID
        /// </summary>
        public string PartnerDBDocumentId { get; set; }
        /// <summary>
        /// Details regarding Invitation's Channel and Reminder-Level
        /// </summary>
        public string AdditionalURLParameter { get; set; }
    }
}
