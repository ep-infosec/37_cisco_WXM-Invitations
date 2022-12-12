using System.Collections.Generic;

namespace XM.ID.Invitations.Net
{
    public static class SharedSettings
    {
        public const string AuthorizationDenied = "Authentication Bearer token in the incoming request header is invalid. Please ensure you are using correct user credentials or a valid Authentication Bearer token.";
        public const string AuthDeniedResponse = "Authorization has been denied for this request.";      
        public const string NoActiveQuestionsFound = "Active Questions not found. Please ensure the dispatch configured on partner hosted side is available in Experience Management.";
        public const string NoDeliveryPlanFound = "Delivery Policy not found. Please ensure the dispatch configured on partner hosted side is available in Experience Management.";
        public const string NoDispatchFound = "Dispatch not found. Please ensure the dispatch configured on partner hosted side is available in Experience Management.";
        public const string NoHashAlgoConfigured = "Algorithm for hashing of PII data is either missing or configured incorrectly. Default hashing algorithm SHA512 is being used for now as fallback. Please check and use a valid algorithm in Experience Management.";
        public const string HashAlgoConfigured = "Hashing algorithm for PII is configured as: ";
        public const string InvalidOrUnsupportedChannels = "An error occurred while using the Delivery Policy configured under Dispatch. Please make sure the Delivery Policy is configured correctly with supported channels.";
        public const string UniQueIdQuestionMissingInDP = "Unique User Identifier(UUID) configured in Delivery Policy used in Dispatch is not available in the questionnaire. This will impact fatigue rules and invites may go out to customers for multiple surveys. Please ensure the UUID is added as a pre-fill question in the questionnaire.";
        public const string BadRequest = "Bad Request.";
        public const string InteralError = "Internal Error";
        public const string NoSurveyQuestionnaireFound = "Survey Questionnaire not found. Please ensure the dispatch configured on partner hosted side is available in Experience Management.";
        public const string NoSettingsFound = "Settings not found. Please ensure the dispatch configured on partner hosted side is available in Experience Management.";
        public const string Sampledrecord = "Record sampled";
        public const string NoSamplingConfigured = "Sampling type is not configured hence all records in the payload are processed. Please ensure sampling type is configured in ACM backend using the \"extendedproperties\" API.";
        public const string NoConfigInSPA = "Dispatch API cannot process the incoming request because the Dispatches are not yet configured in ACM front-end. Please configure this by logging into Account Configuration Module.";
        public const string NoDispatchInSPA = "Dispatch configuration is missing in Account Configuration Module. Please ensure that a valid dispatch is configured in Account Configuration Module and is passed in the API request. \n https://xm.webex.com/docs/cxsetup/guides/acm/.";
        public const string InvalidDispatch = "Dispatch details passed in the API request are not valid. Please ensuredispatch is configured correcty in the Account Configuration Module and correct details are passed in the API request. \n https://xm.webex.com/docs/cxsetup/guides/acm/.";
        public const string PausedDispatch = "Dispatch configured to be used to send invites is paused. Invites will not go out unless this is resolved. Please sign in to Experience Management and un-pause the Dispatch. Also note, any changes in Experience Management may take up to an hour to reflect in Dispatch Request API.";
        public const string NovalidDispatchInTheBatch = "No valid dispatch in the batch found. Please setup a valid dispatch configuration in Account Configuration Module.";
        public const string PausedDP = "Delivery Policy configured under Dispatch to be used to send invites is paused. Invites will not go out unless this is resolved. Please sign in to Experience Management and un-pause the Delivery Policy. Also note, any changes in Experience Management may take up to an hour to reflect in Dispatch Request API.";
        public const string AccountPrefills = "Mandatory account level prefills not found.";
        public const string AllRecordsRejected = "All the records received in the API requests are rejected. Please ensure the channels and UUID are configured correctly in the Delivery policy in Experience Management. Also ensure the Email or SMS values are sent in correct format.";
        public const string AcceptedForProcessing = "Accepted for processing";
        public const string FailDueToEmailOrMobile = "Failed due to invalid Email or mobile number";
        public const string FailDueToUUIDOrChannel = "Failed due to no Common Identifier or Channel";
        public const string DuplicateRecord = "Duplicate record found";
        public const string PayLoadTooLarge = "Payload size is larger than configured limit.";
        public const string APIResponseFail = "Dispatch, Delivery Policy , Questionnaire, Active Questions or Settings not found. Please ensure the dispatch configured on partner hosted side is available in Experience Management.";
        public const string DispatchControllerEx1 = "Exception in DispatchRequest Controller";
        public const string DispatchControllerEx2 = "Exception in ProcessInvitation in DispatchRequest Controller";
        public const string DispatchStatusReturned = "Multiple dispatch status returned in response";
        public const string CheckDispatchIDEx = "Exception in CheckDispatchID";
        public const string CheckDispatchDataEx1 = "Exception in a Dispatch in CheckDispatchData";
        public const string CheckDispatchDataEx2 = "Exception in CheckDispatchData";
        public const string GetChannelFromDPEx = "Exception in GetChannelFromDP";
        public const string CheckAccountLevelPrefills = "Exception in CheckAccountPrefills";
        public const string PrefillsMissing = "Some of the prefills with following question IDs are ignored while processing the records. This may be due to missing question in the questionnaire. Please verify and reconfigure this in the Experience Management.";
        public const string BatchingQueueMissing = "Queue to batch the records for bulk token creation is not found. Please verify and correct the batching queue type using \"extendedProperties\" API in Account Configuration Management.";
        public const string BearerTokenNotGenerated = "Authentication Bearer token not generated. Please ensure you are using correct user credentials.";
        public const string DBUpdateCompleted = "Update to DB completed for bulk token response of size: ";
        public const string BulkTokenException = "Bulk Token API failed due to unknown exception";
        public const string AdminLoginError = "You have signed in with an account other than administrator account. To configure ACM for the first time please sign in using administrator account.";

        public static string GetMaxRecordSizeExceedeed(int limit)
        {
            string MaxDispatchNumberExceeded = $"The API cannot process more than {limit} records in a single request. Please split the batch into multiple requests and try again. Total records received is";
            return MaxDispatchNumberExceeded;
        }

        public static string GetMaxDispatchNumberExceeded(int limit)
        {
            string MaxDispatchNumberExceeded = $"The API cannot process more than {limit} dispatches in a single request. Please ensure dispatch requests are spread out over time. Total dispatches received is";
            return MaxDispatchNumberExceeded;
        }

        public const string ALL_DISPATCH_API_URI = "/api/Dispatches";
        public const string SURVEY_QUESTIONNAIRE = "/api/AllSurveyQuestionnaires";
        public const string ALL_Delivery_ID = "/api/DeliveryPlan";
        public const string ACTIVE_QUES = "/api/Questions/Active";
        public const string BULK_TOKEN_API = "/api/SurveyByToken/Import/Dispatch";
        public const string SETTINGS_API = "/api/settings";
        public const string GET_APIKEY_API = "/api/GetAPIKey";
        public const string GET_DISPATCHES_API = "/api/Dispatches";
        public const string GET_DELIVERY_PLANS_API = "/api/DeliveryPlan";
        public const string GET_ACTIVE_QUES_API = "/api/Questions/Active";
        public const string GET_LOGIN_TOKEN_API = "/api/logintoken";
        public const string GET_DISPATCH_BY_ID_API = "/api/Dispatches/";
        public const string GET_DP_BY_ID_API = "/api/DeliveryPlan/";
        public const string GET_QUES_BY_QNR_API = "/api/Questions/Questionnaire";
        public const string GET_LOGIN_TOKEN = "/api/LoginToken";
        public const string GET_WXM_MERGED_DATA = "/api/DeliveryPlan/WXMMergedData";
        public const string GET_USER_PROFILE = "/api/Profile";
        public const string GET_CONTENT_TEMPLATES = "/api/ContentTemplates";
        

        public static string BASE_URL;
        public static double AuthTokenCacheExpiryInSeconds;
        public static double CacheExpiryInSeconds;

        public static Dictionary<string, ISampler> AvailableSamplers = new Dictionary<string, ISampler>
        {
            { "wxm", new WXMSampler() }
        };
        public static Dictionary<string, IUnsubscribeChecker> AvailableUnsubscribeCheckers = new Dictionary<string, IUnsubscribeChecker>
        {
            { "wxm", new WXMUnsubscribeChecker() }
        };
        public static Dictionary<string, IBatchingQueue<RequestBulkToken>> AvailableQueues = new Dictionary<string, IBatchingQueue<RequestBulkToken>>
        {
            { "inmemory", SingletonConcurrentQueue<RequestBulkToken>.Instance }
        };

    }

}
