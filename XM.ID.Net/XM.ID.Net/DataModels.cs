using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace XM.ID.Net
{
    #region Account Configuration Manager (ACM) Models

    /// <summary>
    /// Stores all necessary details about the account setup configured
    /// for the Global Invitation-Delivery Feature
    /// </summary>
    [BsonIgnoreExtraElements]
    public class AccountConfiguration
    {
        /// <summary>
        /// Unique MongoDB Identifier
        /// </summary>
        [BsonId]
        public string Id { get; set; }
        /// <summary>
        /// Access-Key for WXM
        /// </summary>
        public string WXMAPIKey { get; set; }
        /// <summary>
        /// WXM User
        /// </summary>
        public string WXMUser { get; set; }
        /// <summary>
        /// WXM Base-Url (prod/staging) 
        /// </summary>
        public string WXMBaseURL { get; set; }
        /// <summary>
        /// WXM Account Name
        /// </summary>
        public string WXMAdminUser { get; set; }
        /// <summary>
        /// List of Dispatches available in WXM
        /// </summary>
        public List<DispatchChannel> DispatchChannels { get; set; }
        /// <summary>
        /// List of Vendors available in ACM
        /// </summary>
        public List<Vendor> Vendors { get; set; }
        /// <summary>
        /// Deatils of the Messaging Queue that was configured in WXM
        /// </summary>
        public Queue Queue { get; set; }
        /// <summary>
        /// Stores the details of Custom SMTP settings
        /// </summary>
        public CustomSMTPSetting CustomSMTPSetting { get; set; }
        /// <summary>
        /// Additional details around the account setup done for
        /// the Invitation Delivery Feature
        /// </summary>
        public Dictionary<string, string> ExtendedProperties { get; set; }
        /// <summary>
        /// Questions configured for splits in reports
        /// </summary>
        public List<PrefillSlicing> PrefillsForSlices { get; set; }
    }

    /// <summary>
    /// Configured Prefill for data slicing in reports
    /// </summary>
    [BsonIgnoreExtraElements]
    public class PrefillSlicing
    {
        /// <summary>
        /// Unique question identifier
        /// </summary>
        [Required]
        public string Id { get; set; }
        /// <summary>
        /// question note
        /// </summary>
        [Required]
        public string Note { get; set; }
        /// <summary>
        /// Type of questions
        /// </summary>
        [Required]
        public string DisplayType { get; set; }
        /// <summary>
        /// Question text
        /// </summary>
        [Required]
        public string Text { get; set; }
        /// <summary>
        /// Options configured
        /// </summary>
        [Required]
        public List<string> MultiSelect { get; set; }
    }

    /// <summary>
    /// Describes a Dispatch in terms of configured Channels, Prefills and Notifications
    /// </summary>
    [BsonIgnoreExtraElements]
    public class DispatchChannel
    {
        /// <summary>
        /// Unique Dispatch Identifier
        /// </summary>
        [Required]
        public string DispatchId { get; set; }
        /// <summary>
        /// Name of the Dispatch
        /// </summary>
        [Required]
        public string DispatchName { get; set; }
        /// <summary>
        /// Details about the corresponding Channel (Email/SMS)
        /// </summary>
        [Required]
        public ChannelDetails ChannelDetails { get; set; }
        /// <summary>
        /// Static Question-Answer Values. Used as corresponding Dispatch's PreFills
        /// </summary>
        [Required]
        public List<StaticPrefill> StaticPrefills { get; set; }
        /// <summary>
        /// Subscriber Details for corresponding Dispatch Notifications
        /// </summary>
        [Required]
        public Notify Notify { get; set; }
    }

    /// <summary>
    /// Describes a Channel of a Disaptch in terms of configured Email and SMS Vendors.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class ChannelDetails
    {
        /// <summary>
        /// Details of the Email Channel
        /// </summary>
        [Required]
        public Channel Email { get; set; }
        /// <summary>
        /// Details of the SMS Channel
        /// </summary>
        [Required]
        public Channel Sms { get; set; }
    }

    /// <summary>
    /// Describes a Email/SMS Channel in terms of operational status and configured 3rd party vendor
    /// </summary>
    [BsonIgnoreExtraElements]
    public class Channel
    {
        /// <summary>
        /// Is Channel operational?
        /// </summary>
        [Required]
        public bool IsValid { get; set; }
        /// <summary>
        /// Name of the vendor responsible for Invitation Deliveries via the corresponding Channel
        /// </summary>
        public string Vendorname { get; set; }
    }

    /// <summary>
    /// Describes the prefills of a Dispatch
    /// </summary>
    [BsonIgnoreExtraElements]
    public class StaticPrefill
    {
        /// <summary>
        /// Unique Question Identifier
        /// </summary>
        [Required]
        public string QuestionId { get; set; }
        /// <summary>
        /// Display Name of the Question
        /// </summary>
        [Required]
        public string Note { get; set; }
        /// <summary>
        /// Prefilled answer value of the corresponding question
        /// </summary>
        public string PrefillValue { get; set; }
    }

    /// <summary>
    /// Describes the Notification Subscribers of a Dispatch
    /// </summary>
    [BsonIgnoreExtraElements]
    public class Notify
    {
        /// <summary>
        /// Debug-Level Notification Subscribers
        /// </summary>
        public string D { get; set; }
        /// <summary>
        /// Information-Level Notification Subscribers
        /// </summary>
        public string I { get; set; }
        /// <summary>
        /// Warning-Level Notification Subscribers
        /// </summary>
        public string W { get; set; }
        /// <summary>
        /// Error-Level Notification Subscribers
        /// </summary>
        public string E { get; set; }
        /// <summary>
        /// Failure-Level Notification Subscribers
        /// </summary>
        public string F { get; set; }
    }

    /// <summary>
    /// Describes a Invitation-Delivery Vendor in terms of its Type, Name and Configuration Details
    /// </summary>
    [BsonIgnoreExtraElements]
    public class Vendor
    {
        /// <summary>
        /// "Email" or "Sms"
        /// </summary>
        [Required]
        public string VendorType { get; set; }
        /// <summary>
        /// Name of the vendor. This property is also used as Id
        /// for any search purposes (Case-insensitive matching)
        /// </summary>
        [Required]
        public string VendorName { get; set; }
        /// <summary>
        /// Is Single-Send or Bulk-Send
        /// </summary>
        [Required]
        public bool IsBulkVendor { get; set; }
        /// <summary>
        /// Key-Value Properties regarding the vendor-configuration
        /// </summary>
        [Required]
        public Dictionary<string, string> VendorDetails { get; set; }
    }

    /// <summary>
    /// Stores the details of the Messaging Queue that was configured in WXM
    /// </summary>
    [BsonIgnoreExtraElements]
    public class Queue
    {
        /// <summary>
        /// Azure Queue Storage or AWS Simple Queue Service
        /// </summary>
        public string QueueType { get; set; }
        /// <summary>
        /// Queue Connection-String
        /// </summary>
        public string QueueConnectionString { get; set; }
    }

    /// <summary>
    /// Stores the details of Custom SMTP settings
    /// </summary>
    [BsonIgnoreExtraElements]
    public class CustomSMTPSetting
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Host { get; set; }
        public string Port { get; set; }
        public string SenderEmailAddress { get; set; }
        public string SenderName { get; set; }
        public string EnableSsl { get; set; }
    }

    /// <summary>
    /// Represents the ACM-Login-API request body
    /// </summary>
    public class SPALoginRequest
    {
        /// <summary>
        /// Username of the WXM User
        /// </summary>
        [Required]
        public string Username { get; set; }
        /// <summary>
        /// SecretKey of the WXM User
        /// </summary>
        [Required]
        public string Password { get; set; }
    }

    /// <summary>
    /// Represents the ACM-Login-API response body
    /// </summary>
    public class ACMLoginResponse
    {
        /// <summary>
        /// Property stating whether login was successful or not?
        /// </summary>
        public bool IsSuccessful { get; set; }
        /// <summary>
        /// Bearer Token if login was successful.
        /// Error Message if login was unsuccessful.
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// Represents the generic result sent back from any ACM-API that is not the ACM-Login-API
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ACMGenericResult<T>
    {
        /// <summary>
        /// HTTP Response Body
        /// </summary>
        public T Value { get; set; }
        /// <summary>
        /// HTTP Response StatusCode
        /// </summary>
        public int StatusCode { get; set; }
    }

    /// <summary>
    /// Stores details of DispatchIds and their corresponding DispatchNames
    /// along with details of the configured messaging queue in WXM
    /// </summary>
    public class DispatchesAndQueueDetails
    {
        /// <summary>
        /// Represents a list of mapping between the DispatchIds and the DispatchNames
        /// </summary>
        public List<KeyValuePair<string, string>> Dispatches { get; set; }
        /// <summary>
        /// Details of the Message Queue that was configured in WXM
        /// </summary>
        public Queue Queue { get; set; }
    }

    /// <summary>
    /// Event Log Object.
    /// </summary>
    public class EventLogObject
    {
        public int NumberofRows { get; set; }
        public int MaxReminders { get; set; }
        public List<EventLogs> EventLogs { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class EventLogs
    {
        public string TokenID { get; set; }
        public string Dispatch { get; set; }
        public string DispatchID { get; set; }
        public string Questionnaire { get; set; }
        public string BatchId { get; set; }
        public string UUID { get; set; }
        public string RecordStatus { get; set; }
        public string RecordRejectReason { get; set; }
        public string TokenCreationTime { get; set; }
        public string DPDispatchStatus { get; set; }
        public string DPDispatchTime { get; set; }
        public string DPRejectReason { get; set; }
        public string Channel { get; set; }
        public string DispatchVendor { get; set; }
        public string DispatchStatus { get; set; }
        public string DispatchRejectReason { get; set; }
        public string DispatchTime { get; set; }
        public List<EventReminderLog> Reminder { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class EventReminderLog
    {
        public int ReminderNumber { get; set; }
        public string Channel { get; set; }
        public string ReminderTime { get; set; }
        public string ReminderDPStatus { get; set; }
        public string ReminderDispatchStatus { get; set; }
    }

    #endregion

    #region WXM Models

    public class BearerToken
    {
        [JsonProperty(PropertyName = "access_token")]
        public string AccessToken { get; set; }

        [JsonProperty(PropertyName = "token_type")]
        public string TokenType { get; set; }

        [JsonProperty(PropertyName = "expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty(PropertyName = "userName")]
        public string UserName { get; set; }

        [JsonProperty(PropertyName = "email")]
        public string Email { get; set; }

        [JsonProperty(PropertyName = "primaryRole")]
        public string PrimaryRole { get; set; }

        [JsonProperty(PropertyName = "managedBy")]
        public string ManagedBy { get; set; }

        [JsonProperty(PropertyName = "preview")]
        public string Preview { get; set; }

        [JsonProperty(PropertyName = "station")]
        public string Station { get; set; }

        [JsonProperty(PropertyName = "hash")]
        public string Hash { get; set; }

        [JsonProperty(PropertyName = ".issued")]
        public string Issued { get; set; }

        [JsonProperty(PropertyName = ".expires")]
        public string Expires { get; set; }
    }

    public class Dispatch
    {
        public string Id { get; set; }
        public string User { get; set; }
        public string Name { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public string DeliveryPlanId { get; set; }
        public Dictionary<string, string> ContentTemplateIds { get; set; }
        public string TokenTemplateId { get; set; }
        public string QuestionnaireName { get; set; }
        public string QuestionnaireDisplayName { get; set; }
        public bool IsLive { get; set; }
        public string Message { get; set; }
    }

    public class Schedule
    {
        public string onChannel { get; set; }
        public int paceConnections { get; set; }
        public bool? nonConCurrent { get; set; }
        public int delayByHours { get; set; }
        public string subject { get; set; }
        public string textBody { get; set; }
        public string htmlBody { get; set; }
        public string templateId { get; set; }
        public string externalDNDCheck { get; set; }
        public string additionalURLParameter { get; set; }
    }

    public class RouteEmailSMTP
    {
        public string fromName { get; set; }
        public string fromAddress { get; set; }
        public string server { get; set; }
        public string login { get; set; }
        public string password { get; set; }
        public int port { get; set; }
        public bool? enableSSL { get; set; }
    }

    public class DeliveryPlan
    {
        public string id { get; set; }
        public string user { get; set; }
        public string outboundResidency { get; set; }
        public DateTime created { get; set; }
        public DateTime updated { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public bool isLive { get; set; }
        public DateTime? goodAfterTimeOfDay { get; set; }
        public DateTime? goodBeforeTimeOfDay { get; set; }
        public List<string> goodOnDaysOfWeek { get; set; }
        public DateTime? startAfter { get; set; }
        public DateTime? endBefore { get; set; }
        public int remindOnlyAfterHours { get; set; }
        public bool? remindOnlyAfterOpenHours { get; set; }
        public int repeatOnlyAfterHours { get; set; }
        public int repeatOnlyLessThanResponses { get; set; }
        public int remindOnlyLessThanInvites { get; set; }
        public string uniqueCustomerIDByPreFilledQuestionTag { get; set; }
        public bool uniquieIDAccountWide { get; set; }
        public List<Schedule> schedule { get; set; }
        public bool notifyOnExceptions { get; set; }
        public List<string> notifyToEmailIds { get; set; }
        public string emailFromAddress { get; set; }
        public string emailFromName { get; set; }
        public string whitelabelSurveyDomainPath { get; set; }
        public List<string> maskQuestionIdsOnCollection { get; set; }
        public List<string> ommitQuestionIdsOnCollection { get; set; }
        public RouteEmailSMTP routeEmailSMTP { get; set; }
        public int tokensAttached { get; set; }
        public int surveysDelivered { get; set; }
        public int surveysOpened { get; set; }
        public int surveysAnswered { get; set; }
        public int remindersDelivered { get; set; }
        public int emailSent { get; set; }
        public int emailBounces { get; set; }
        public int unsubscribes { get; set; }
        public int smsSent { get; set; }
        public int deliveryExceptions { get; set; }
        public int expiredTokens { get; set; }
        public int duplicateSupressed { get; set; }
        public bool overrideEarlierPendingRequests { get; set; }
        public DateTime lastRun { get; set; }
        public string status { get; set; }
        public string message { get; set; }
    }

    public class Settings
    {
        public string id { get; set; }
        public string user { get; set; }
        public bool locationDataMigrated { get; set; }
        public List<Location> locationList { get; set; }
        public Integration Integrations { get; set; }
        public int TimeZoneOffset { get; set; }
    }

    public class Profile
    {
        public string user { get; set; }
        public string name { get; set; }
        public int? timeZoneOffset { get; set; }
    }



    public class Integration
    {
        public List<QueueChannel> QueueDetails { get; set; }
    }

    public class QueueChannel
    {
        public string Type { get; set; } //channel type azurequeue / Azure service bus / aws Queue
        public string QueueName { get; set; }
        public string ConnectionString { get; set; }
    }

    public class Location
    {
        /// <summary>
        /// If true, means that the 'Name' property acts as an immutable Identifier, and 'DisplayName' property should be used for modifiable display and other cosmetic properties.
        /// If false, means that the 'Name' property can be used both as an Identifier, as well as for display purposes.
        /// </summary>
        public bool IsNameImmutable { get; set; } = false;

        /// <summary>
        /// Location Name (ex: Downtown)
        /// </summary>
        [Required]
        public string Name { get; set; }
        /// <summary>
        /// This can be renamed everyday for UI/reports and is only for cosmetic display
        /// </summary>
        public string DisplayName { get; set; }
        /// <summary>
        /// Address for map
        /// </summary>
        public string Address { get; set; }
        /// <summary>
        /// Brand Name (ex: Orange)
        /// </summary>
        public string Brand { get; set; }
        /// <summary>
        /// Displayed Poll Channels
        /// </summary>
        public List<string> PollChannels { get; set; }
        /// <summary>
        /// Logo Set
        /// </summary>
        public string LogoURL { get; set; }
        /// <summary>
        /// Background URL set
        /// </summary>
        [DataType(DataType.ImageUrl)]
        public string BackgroundURL { get; set; }

        /// <summary>
        /// Background URL set
        /// </summary>
        [DataType(DataType.ImageUrl)]
        public string BusinessURL { get; set; }

        /// <summary>
        /// ex: South, North
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Location Tags
        /// </summary>
        public List<string> Tags { get; set; }

        /// <summary>
        /// Hex Color Code
        /// </summary>
        public string ColorCode1 { get; set; }
        /// <summary>
        /// Hex Color Code
        /// </summary>
        public string ColorCode2 { get; set; }
        /// <summary>
        /// Hex Color Code
        /// </summary>
        public string ColorCode3 { get; set; }

        /// <summary>
        /// Welcome title on questionnaire start
        /// </summary>
        public string WelcomeTitle { get; set; } // "Please help us understand .."
        /// <summary>
        /// Welcome text on questionnaire start
        /// </summary>
        public string WelcomeText { get; set; } // "Please help us understand .."
        /// <summary>
        /// Thank you title on questionnaire end
        /// </summary>
        public string ThankyouTitle { get; set; } // "Please help us understand .."

        /// <summary>
        /// Thank you title on questionnaire end
        /// </summary>
        /// <remarks>Back Compat Only, Pending Remove Post Porting</remarks>
        [Obsolete]
        public string ThankyouTtitle { get; set; } // "Please help us understand .."

        /// <summary>
        /// Thank you text on questionnaire end
        /// </summary>
        public string ThankyouText { get; set; } // "Please help us understand .."
        /// <summary>
        /// Welcome Audio
        /// </summary>
        public string WelcomeAudio { get; set; } // Optional Audio For IVRS
        /// <summary>
        /// Thankyou Audio
        /// </summary>
        public string ThankyouAudio { get; set; } // Optional Audio For IVRS
        /// <summary>
        /// Welcome Disclaimer Text
        /// </summary>
        public string WelcomeDisclaimerText { get; set; } // Optional Welcome Disclaimer Text
        /// <summary>
        /// Thank you Disclaimer Text
        /// </summary>
        public string ThankyouDisclaimerText { get; set; } // Optional Thank you Disclaimer Text
        /// <summary>
        /// Redirect to URL on survey completion
        /// </summary>
        public string RedirectOnSubmit { get; set; }
        /// <summary>
        /// Custom Attributes Per Location
        /// </summary>
        public Dictionary<string, string> Attributes { get; set; }

        /// <summary>
        /// Multi Language Support, ISO 639-1 Code(ex: en = > english), Translated Display Item
        /// </summary>
        public Dictionary<string, AltDisplaySettings> Translated { get; set; }

        /// <summary>
        /// Data Retention Limit For Location
        /// </summary>
        /// <remarks>Min = 7 Days, Max = 1095 Days(3 Years)</remarks>
        public int DataRetentionDays { get; set; }

        /// <summary>
        /// Contains details for each themes, Ex. custom dictionnary
        /// </summary>
        public List<ThemeDetails> ThemeDictionary { get; set; }

        /// <summary>
        /// If Specified hash the response on collecting with algorithm specified EX: sha256 or sha384 or sha512
        /// after converting the <text> to lowercase, hash the responses in the format sha256:<text> or sha384:<text>
        /// </summary>
        public string HashPIIBy { get; set; }
    }

    public class AltDisplaySettings
    {
        /// <summary>
        /// Optional Welcome Intro/Title
        /// </summary>
        public string WelcomeTitle { get; set; }

        /// <summary>
        /// Ex: "Please help us understand .."
        /// </summary>
        public string WelcomeText { get; set; }
        /// <summary>
        /// Optional Audio For IVRS
        /// </summary>
        public string WelcomeAudio { get; set; }
        /// <summary>
        /// Welcome Disclaimer Text
        /// </summary>
        public string WelcomeDisclaimerText { get; set; } // Optional Welcome Disclaimer Text
        /// <summary>
        /// Thank you message title
        /// </summary>
        public string ThankyouTitle { get; set; }
        /// <summary>
        /// Thank you message text
        /// </summary>
        public string ThankyouText { get; set; }
        /// <summary>
        /// Optional Audio For IVRS
        /// </summary>
        public string ThankyouAudio { get; set; }
        /// <summary>
        /// Thank you Disclaimer Text
        /// </summary>
        public string ThankyouDisclaimerText { get; set; } // Optional Thank you Disclaimer Text
        /// <summary>
        /// ex: "Information provided here is kept .."
        /// </summary>
        public string DisclaimerText { get; set; }
    }

    public class ThemeDetails
    {
        public string Name { get; set; }
        public List<string> Tags { get; set; }
        public int Weight { get; set; }
    }

    public class Question
    {
        public string Id { get; set; }
        public string User { get; set; }
        public DateTime? LastUpdate { get; set; }
        public string LastAuthor { get; set; }
        public string SetName { get; set; }
        public int Sequence { get; set; }
        public string Text { get; set; }
        public string TitleText { get; set; }
        public string Audio { get; set; }
        public string DisplayType { get; set; }
        public List<string> MultiSelect { get; set; }
        public List<string> DisplayLegend { get; set; }
        public List<string> MultiSelectChoiceTag { get; set; }
        public bool StaffFill { get; set; }
        public bool ApiFill { get; set; }
        public List<string> DisplayLocation { get; set; }
        public List<string> DisplayLocationByTag { get; set; }
        public double UserWeight { get; set; }
        public string DisplayStyle { get; set; }
        public object ConditionalToQuestion { get; set; }
        public object ConditionalAnswerCheck { get; set; }
        public int ConditionalNumber { get; set; }
        public bool EndOfSurvey { get; set; }
        public string EndOfSurveyMessage { get; set; }
        public string PresentationMode { get; set; }
        public object AnalyticsTag { get; set; }
        public bool IsRequired { get; set; }
        public List<string> QuestionTags { get; set; }
        public List<string> TopicTags { get; set; }
        public DateTime? GoodAfter { get; set; }
        public DateTime? GoodBefore { get; set; }
        public DateTime? TimeOfDayAfter { get; set; }
        public DateTime? TimeOfDayBefore { get; set; }
        public bool IsRetired { get; set; }
        public string Note { get; set; }
        public PIISetting piiSettings { get; set; }
        public List<string> MappedHeaderTags { get; set; }
        public List<QuestionOverrideAttributes> PerLocationOverride { get; set; }
    }

    public class QuestionOverrideAttributes
    {
        public string Location { get; set; }
        public List<string> MappedHeaderTags { get; set; }
        public bool? StaffFill { get; set; }
        public bool? ApiFill { get; set; }
    }

    public class PIISetting
    {
        public bool isPII { get; set; } // set a question as PII 
        public string piiType { get; set; }
        public string exceptionBy { get; set; }
        public DateTime? exceptionAt { get; set; }
    }

    public class WXMMergedEventsFilter
    {
        public FilterBy filter { get; set; }
        public List<string> TokenIds { get; set; }
    }

    public class ContentTemplate
    {
        public string Id { get; set; } // Template Id
        public string Account { get; set; }
        public string Name { get; set; }
    }

    public class WXMDeliveryEvents
    {
        public List<Response> Responses { get; set; }
        public string _id { get; set; }
        public DateTime AnsweredAt { get; set; }
        public bool Partial { get; set; }
        public List<DeliveryEvent> WXMEvents { get; set; }
    }

    public class DeliveryEvent
    {
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// Email, MicroCherry, Bot, SMS or Survey(Cross-Channel)
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Sent, Bounced, Unsubscribed, Displayed(Survey open), Open(email), Answered(survey submit)
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Optional Note, Subject for Email, ex : Rated NPS 7, SMTP Error, Email Bounced
        /// </summary>
        public string Message { get; set; }
        public int? SentSequence { get; set; } //0= Initial invite, 1 = First reminder, 2= Second reminder
        public string MessageTemplate { get; set; }
        public string LogMessage { get; set; } //to store log message in case of dispatchsucccess or unsuccess
    }

    [BsonIgnoreExtraElements(true)]
    public class Response
    {
        /// <summary>
        /// Question ID of Presented Question
        /// </summary>
        [Required]
        public string QuestionId { get; set; }
        /// <summary>
        /// Question Text as When Presented
        /// </summary>
        [Required]
        public string QuestionText { get; set; }
        /// <summary>
        /// Text Input If Question Accepts Text
        /// </summary>
        public string TextInput { get; set; }
        /// <summary>
        /// Text Input If Question Accepts Number
        /// </summary>
        public int NumberInput { get; set; }

        //[Obsolete]
        //public int Comparator { get; set; } // Comparator - 0(AND), 1(AND)
    }

    public class UserProfile
    {
        public int? TimeZoneOffset { get; set; }
    }

    #endregion

    #region DispatchApiRequest Models
    public class DispatchRequest
    {
        public List<List<PreFillValue>> PreFill { get; set; }
        public string DispatchID { get; set; }
    }

    public class RequestPrefill
    {
        public List<List<PreFillValue>> PreFill { get; set; }
        public string DeliveryPlanID { get; set; }
        public List<string> Channels { get; set; }
        public string UniqueCustomerIDByPreFilledQuestionTag { get; set; }
        public string QuestionnaireName { get; set; }
        public string PrimaryChannel { get; set; }
    }

    public class PreFillValue
    {
        public string QuestionId { get; set; }
        public string Input { get; set; }
    }

    public class BatchResponse
    {
        public string BatchId { get; set; }
        public List<StatusByDispatch> StatusByDispatch { get; set; }
    }
    public class StatusByDispatch
    {
        public string DispatchId { get; set; }
        public string Message { get; set; }
        public string DispatchStatus { get; set; }
    }

    public class FilterBy
    {
        public DateTime afterdate { get; set; }
        public DateTime beforedate { get; set; }
    }

    public class ACMInputFilter
    {
        public string afterdate { get; set; }
        public string beforedate { get; set; }
    }

    #endregion

    #region DP Reporting Logging models

    public class OnDemandReportModel
    {
        [BsonId]
        public string Id { get; set; }
        public FilterBy Filter { get; set; }
        public bool IsLocked { get; set; }
        public int TimeOffSet { get; set; }
        public bool OnlyLogs { get; set; }
        //public bool IsMerging { get; set; }
    }

    public class WXMPartnerMerged
    {
        public List<Response> Responses { get; set; }
        public DateTime AnsweredAt { get; set; }
        public bool Answered { get; set; }
        public string _id { get; set; }
        public string BatchId { get; set; }
        public string DispatchId { get; set; }
        public string DeliveryWorkFlowId { get; set; }
        public bool Requested { get; set; }
        public string RequestedChannel { get; set; }
        public string RequestedMessage { get; set; }
        public bool TokenCreated { get; set; }
        public string TokenCreatedChannel { get; set; }
        public string TokenCreatedMessage { get; set; }
        public bool Rejected { get; set; }
        public string RejectedChannel { get; set; }
        public string RejectedMessage { get; set; }
        public bool Error { get; set; }
        public string ErrorChannel { get; set; }
        public string ErrorMessage { get; set; }
        public bool Supressed { get; set; }
        public string SupressedChannel { get; set; }
        public string SupressedMessage { get; set; }
        public bool Throttled { get; set; }
        public string ThrottledChannel { get; set; }
        public string ThrottledMessage { get; set; }
        public bool Sent { get; set; } // will be true even if one message in the sequence was sent
        public bool Partial { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public string TargetHashed { get; set; }
        public string Questionnaire { get; set; }
        public bool Unsubscribe { get; set; }
        public string UnsubscribeChannel { get; set; }
        public string UnsubscribeMessage { get; set; }
        public bool Unsubscribed { get; set; }
        public string UnsubscribedChannel { get; set; }
        public string UnsubscribedMessage { get; set; }
        public bool Bounced { get; set; }
        public string BouncedChannel { get; set; }
        public string BouncedMessage { get; set; }
        public bool Exception { get; set; }
        public int ExceptionCount { get; set; }
        public string ExceptionChannel { get; set; }
        public string ExceptionMessage { get; set; }
        public bool Displayed { get; set; }
        public string DisplayedChannel { get; set; }
        public string DisplayedMessage { get; set; }
        public string SentSequence { get; set; }
        public List<DeliveryEvent> Events { get; set; }
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

    public class ScheduleReportSettings
    {
        public bool ScheduleReport { get; set; }
        public double Frequency { get; set; }
        public string StartDate { get; set; }
        public int ReportForLastDays { get; set; }
        public bool AutoPickLastStartDate { get; set; }
    }

    public class DataUploadSettings
    {
        public double RunUploadEveryMins { get; set; }
        public double UploadDataForLastHours { get; set; }
        public double CheckResponsesCapturedForLastHours { get; set; }
    }


    public class DeliveryEventsByTarget
    {
        public string id { get; set; }
        public string deliveryWorkFlowId { get; set; }
        public string user { get; set; }
        public string location { get; set; }
        public DateTime created { get; set; }
        public DateTime updated { get; set; }
        public List<Event> events { get; set; }
        public string target { get; set; }
        public string dispatchId { get; set; }

    }

    public class Event
    {
        public DateTime timeStamp { get; set; }
        public string channel { get; set; }
        public string action { get; set; }
        public int? SentSequence { get; set; }
        public string message { get; set; }

    }
    #endregion

    #region Request Initiator Models
    public class RequestInitiatorRecords
    {
        /// <summary>
        /// Unique MongoDB Identifier
        /// </summary>
        [BsonId]
        public string Id { get; set; }
        public string DisplayFileName { get; set; }
        public string FileName { get; set; }
        public string NoOfBatches { get; set; }
        public string BatchId { get; set; }
        public DateTime CreatedOn { get; set; }
    }
    #endregion

    #region NTLM Email Service
    public class RequestBody
    {
        public string EmailId { get; set; }
        public string Subject { get; set; }
        public string TextBody { get; set; }
        public string HTMLBody { get; set; }
    }
    #endregion
}
