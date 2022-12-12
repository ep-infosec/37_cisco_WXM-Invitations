using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace XM.ID.Net
{
    public class WXMService
    {
        private const string ALL_DISPATCH_API_URI = "/api/Dispatches";
        private const string SURVEY_QUESTIONNAIRE = "/api/AllSurveyQuestionnaires";
        private const string ALL_Delivery_ID = "/api/DeliveryPlan";
        private const string ACTIVE_QUES = "/api/Questions/Active";
        private const string BULK_TOKEN_API = "/api/SurveyByToken/Import/Dispatch";
        private const string SETTINGS_API = "/api/settings";
        private const string GET_PROFILE = "/api/Profile";
        private const string GET_APIKEY_API = "/api/GetAPIKey";
        private const string GET_DISPATCHES_API = "/api/Dispatches";
        private const string GET_DELIVERY_EVENT_BY_TARGET = "/api/DeliveryEventsBy/Target";
        private const string GET_DELIVERY_PLANS_API = "/api/DeliveryPlan";
        private const string GET_ACTIVE_QUES_API = "/api/Questions/Active";
        private const string GET_LOGIN_TOKEN_API = "/api/logintoken";
        private const string GET_DISPATCH_BY_ID_API = "/api/Dispatches/";
        private const string GET_DP_BY_ID_API = "/api/DeliveryPlan/";
        private const string GET_QUES_BY_QNR_API = "/api/Questions/Questionnaire";
        private const string GET_LOGIN_TOKEN = "/api/LoginToken";
        public const string LoginAPIErrorTwoFactor = "two_factor";
        public const string LoginAPIErrorInvalidSecureCode = "Invalid Two Factor Secure Code, Check For Code Sent To Your Email/Phone Or Contact Your Administrator";
        public static string LoginAPIErrorTwoFactorSecureCode = "Valid Two Factor Secure Code Required, Enter Code Received";
        public static string LoginAPIUserBlocked = "Your account has been locked out. Please contact your administrator for a fresh password or try reset password";
        public static string InvalidOTP = "Invalid OTP";
        private string BASE_URL;

        private readonly HttpClient HttpClient;

        public WXMService(string baseUrl)
        {
            HttpClient = new HttpClient();
            BASE_URL = baseUrl;
        }

        public async Task<BearerToken> GetLoginToken(string username, string password)
        {
            string requestUri = BASE_URL + GET_LOGIN_TOKEN_API;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            List<KeyValuePair<string, string>> requestPostValues = new List<KeyValuePair<string, string>>
            {
                { new KeyValuePair<string,string>("grant_type", "password") },
                { new KeyValuePair<string,string>("username", username) },
                { new KeyValuePair<string,string>("password", password) }
            };
            request.Content = new FormUrlEncodedContent(requestPostValues);
            HttpResponseMessage response = await HttpClient.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                string error = await response.Content.ReadAsStringAsync();
                var errorObject = JsonConvert.DeserializeObject<Dictionary<string, string>>(error);
                if (errorObject["error"] == LoginAPIErrorTwoFactor &&
                    errorObject["error_description"] == LoginAPIErrorTwoFactorSecureCode)
                    throw new System.Exception(LoginAPIErrorTwoFactorSecureCode);
                else if (errorObject["error"] == LoginAPIErrorTwoFactor &&
                    errorObject["error_description"] == LoginAPIErrorInvalidSecureCode)
                    throw new System.Exception(InvalidOTP);
                else if (errorObject["error"] == "Authorization Error" &&
                    errorObject["error_description"] == LoginAPIUserBlocked)
                    throw new System.Exception(LoginAPIUserBlocked);
                else
                    return default;
            }
            else if (!response.IsSuccessStatusCode)
            {
                return default;
            }
            string stringBearerToken = await response.Content.ReadAsStringAsync();
            BearerToken bearerToken = JsonConvert.DeserializeObject<BearerToken>(stringBearerToken);
            return bearerToken;
        }

        private async Task<T> MakeHttpRequestAsync<T>(string bearerToken, string httpMethod, string requestUri, string jsonBody = null)
        {
            HttpRequestMessage request = httpMethod switch
            {
                "POST" => new HttpRequestMessage(HttpMethod.Post, requestUri),
                "GET" => new HttpRequestMessage(HttpMethod.Get, requestUri),
                "PUT" => new HttpRequestMessage(HttpMethod.Put, requestUri),
                "DELETE" => new HttpRequestMessage(HttpMethod.Delete, requestUri),
                _ => new HttpRequestMessage(HttpMethod.Options, requestUri),
            };
            request.Headers.Add("Authorization", bearerToken);
            if (!string.IsNullOrWhiteSpace(jsonBody))
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return default;
            }
            string stringResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(stringResponse);
        }

        public async Task<string> GetAPIKey(string bearerToken)
        {
            string uri = BASE_URL + GET_APIKEY_API;
            return await MakeHttpRequestAsync<string>(bearerToken, "GET", uri);
        }

        public async Task<List<DeliveryEventsByTarget>> GetDeliveryEventsBy(string bearerToken, string targetid)
        {
            string uri = BASE_URL + GET_DELIVERY_EVENT_BY_TARGET + "/" + targetid;
            return await MakeHttpRequestAsync<List<DeliveryEventsByTarget>>(bearerToken, "GET", uri);
        }

        public async Task<List<Dispatch>> GetDispatches(string bearerToken)
        {
            string uri = BASE_URL + GET_DISPATCHES_API;
            return await MakeHttpRequestAsync<List<Dispatch>>(bearerToken, "GET", uri);
        }

        public async Task<List<DeliveryPlan>> GetDeliveryPlans(string bearerToken)
        {
            string uri = BASE_URL + GET_DELIVERY_PLANS_API;
            return await MakeHttpRequestAsync<List<DeliveryPlan>>(bearerToken, "GET", uri);
        }

        public async Task<List<Question>> GetActiveQuestions(string bearerToken)
        {
            string uri = BASE_URL + GET_ACTIVE_QUES_API;
            return await MakeHttpRequestAsync<List<Question>>(bearerToken, "GET", uri);
        }
        public async Task<Profile> GetProfile(string bearerToken)
        {
            string uri = BASE_URL + GET_PROFILE;
            return await MakeHttpRequestAsync<Profile>(bearerToken, "GET", uri);
        }

        public async Task<Settings> GetSettings(string bearerToken)
        {
            string uri = BASE_URL + SETTINGS_API;
            return await MakeHttpRequestAsync<Settings>(bearerToken, "GET", uri);
        }

        public async Task<Dispatch> GetDispatchById(string bearerToken, string dispatchId)
        {
            string uri = BASE_URL + GET_DISPATCH_BY_ID_API + dispatchId;
            return await MakeHttpRequestAsync<Dispatch>(bearerToken, "GET", uri);
        }

        public async Task<DeliveryPlan> GetDeliveryPlanById(string bearerToken, string deliveryPlanId)
        {
            string uri = BASE_URL + GET_DP_BY_ID_API + deliveryPlanId;
            return await MakeHttpRequestAsync<DeliveryPlan>(bearerToken, "GET", uri);
        }

        public async Task<List<Question>> GetQuestionsByQNR(string bearerToken, string qnr)
        {
            string uri = BASE_URL + GET_QUES_BY_QNR_API;
            Dictionary<string, string> body = new Dictionary<string, string>
            {
                {"name", qnr }
            };
            string jsonBody = JsonConvert.SerializeObject(body);
            return await MakeHttpRequestAsync<List<Question>>(bearerToken, "POST", uri, jsonBody);
        }
    }
}
