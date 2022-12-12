using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Invitations.Net
{
    public class HTTPWrapper
    {
        private readonly string _batchID;
        private readonly EventLogList _EventLogList;

        public HTTPWrapper()
        {
            _batchID = string.Empty;
            _EventLogList = null;
        }

        public HTTPWrapper(string batchID, EventLogList eventLogList)
        {
            _batchID = batchID;
            _EventLogList = eventLogList;
        }

        public async Task<string> GetActiveQuestions(string FinalToken)
        {
            try
            {
                string responseBody = await SendAsync(SharedSettings.BASE_URL + SharedSettings.ACTIVE_QUES, FinalToken);

                if (!string.IsNullOrEmpty(responseBody))
                    return responseBody;
                else
                    //throw new Exception(SharedSettings.NoQuestionnaireFound);
                    return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<string> GetAllDispatchInfo(string FinalToken)
        {
            try
            {
                string responseBody = await SendAsync(SharedSettings.BASE_URL + SharedSettings.ALL_DISPATCH_API_URI, FinalToken);

                if (!string.IsNullOrEmpty(responseBody))
                    return responseBody;
                else
                    //throw new Exception(SharedSettings.NoDispatchFound);
                    return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<string> GetSurveyQuestionnaire(string FinalToken)
        {
            try
            {
                string responseBody = await SendAsync(SharedSettings.BASE_URL + SharedSettings.SURVEY_QUESTIONNAIRE
                    , FinalToken);

                if (!string.IsNullOrEmpty(responseBody))
                    return responseBody;
                else
                    //throw new Exception(SharedSettings.NoDispatchFound);
                    return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<string> GetDeliveryPlans(string FinalToken)
        {
            try
            {
                string responseBody = await SendAsync(SharedSettings.BASE_URL + SharedSettings.ALL_Delivery_ID, FinalToken);

                if (!string.IsNullOrEmpty(responseBody))
                    return responseBody;
                else
                    //throw new Exception(SharedSettings.NoDeliveryPlanFound);
                    return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        public async Task<(string, List<BulkTokenResult>)> BulkTokenAPI(string FinalToken, RequestBulkToken reqbulktoken)
        {
            try
            {
                string bulktokenjson = JsonConvert.SerializeObject(reqbulktoken);

                string responseBody = await SendAsync(SharedSettings.BASE_URL + SharedSettings.BULK_TOKEN_API, FinalToken, bulktokenjson);

                if (!string.IsNullOrEmpty(responseBody))
                {
                    var response = JsonConvert.DeserializeObject<Dictionary<string, List<Response>>>(responseBody);
                    if (response != null && response.Count > 0)
                    {
                        List<BulkTokenResult> bulkResult = new List<BulkTokenResult>();
                        foreach (var r in response)
                        {
                            var uniqueId = r.Value?.Find(x => x.QuestionId == reqbulktoken.UUID)?.TextInput;

                            var batchid = r.Value?.Find(x => x.QuestionId == reqbulktoken.Batchid)?.TextInput;
                            bulkResult.Add(new BulkTokenResult() { Token = r.Key, UUID = uniqueId, Batchid = batchid });

                        }
                        return (reqbulktoken.DispatchId, bulkResult);
                    }
                    else
                        return (reqbulktoken.DispatchId, null);

                }
                else
                    return (reqbulktoken.DispatchId, null);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<string> GetSettings(string FinalToken)
        {
            try
            {
                string responseBody = await SendAsync(SharedSettings.BASE_URL + SharedSettings.SETTINGS_API, FinalToken);

                if (!string.IsNullOrEmpty(responseBody))
                    return responseBody;
                else
                    return null; // if no response, considering invalid user
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        //heavy object so not caching
        //All WXM related data that is necessary for reporting
        public async Task<List<WXMDeliveryEvents>> GetWXMOperationMetrics(WXMMergedEventsFilter filter, string bearer)
        {
            if (filter == null || bearer == null)
                return null;

            try
            {
                var client = new HttpClient();

                var json = JsonConvert.SerializeObject(filter);
                var data = new StringContent(json, Encoding.UTF8, "application/json");

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.PostAsync(SharedSettings.BASE_URL + SharedSettings.GET_WXM_MERGED_DATA, data);

                if ((int)response.StatusCode == 200)
                {
                    string result = response.Content.ReadAsStringAsync().Result;

                    return JsonConvert.DeserializeObject<List<WXMDeliveryEvents>>(result);
                }

                return null;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<string> GetUserProfile(string FinalToken)
        {
            try
            {
                string responseBody = await SendAsync(SharedSettings.BASE_URL + SharedSettings.GET_USER_PROFILE, FinalToken);

                if (!string.IsNullOrEmpty(responseBody))
                    return responseBody;
                else
                    return null; // if no response, considering invalid user
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<string> GetContentTemplates(string FinalToken)
        {
            try
            {
                string responseBody = await SendAsync(SharedSettings.BASE_URL + SharedSettings.GET_CONTENT_TEMPLATES, FinalToken);

                if (!string.IsNullOrEmpty(responseBody))
                    return responseBody;
                else
                    return null; // if no response, considering invalid user
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<string> GetLoginToken(string username, string apikey)
        {
            try
            {
                HttpResponseMessage responseMessage;
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, SharedSettings.BASE_URL + SharedSettings.GET_LOGIN_TOKEN);
                var postvalues = new[] {
                    new KeyValuePair<string, string> ("grant_type", "password"),
                    new KeyValuePair<string, string> ("username", username),
                    new KeyValuePair<string, string> ("password", apikey)
                };
                string responseBody = string.Empty;
#pragma warning disable IDE0067 // Dispose objects before losing scope
                HttpClient httpClient = new HttpClient();
#pragma warning restore IDE0067 // Dispose objects before losing scope
                requestMessage.Content = new FormUrlEncodedContent(postvalues);
                responseMessage = await httpClient.SendAsync(requestMessage);

                if (responseMessage != null)
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        responseBody = await responseMessage.Content.ReadAsStringAsync();
                        return responseBody;
                    }
                    else if (responseMessage.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        //Throw exception in case of authorization denied
                        throw new Exception(SharedSettings.AuthorizationDenied);
                    }
                    else
                        return null;
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// To send async api request
        /// </summary>
        /// <param name="url">API url</param>
        /// <param name="bearerToken">Token for authorization</param>
        /// <param name="jsonBody">Json body to be send in request.</param>
        /// <returns>API response</returns>
        internal async Task<string> SendAsync(string url, string bearerToken, string jsonBody = null)
        {
            try
            {
                //To check whether url is not null or empty
                if (string.IsNullOrEmpty(url))
                    throw new Exception("API url is either null or empty");
                if (string.IsNullOrEmpty(bearerToken))
                    throw new Exception("Authorization Token is either null or empty");
                //Varible declarations.
                string responseBody = string.Empty;
#pragma warning disable IDE0067 // Dispose objects before losing scope
                HttpClient httpClient = new HttpClient();
#pragma warning restore IDE0067 // Dispose objects before losing scope
                HttpRequestMessage requestMessage;
                HttpResponseMessage responseMessage;

                //To check whether the request is Post or Get
                if (!string.IsNullOrEmpty(jsonBody))
                {
                    requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                    };
                }
                else
                    requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

                requestMessage.Headers.Add("Authorization", bearerToken);

                //Sending api request to CC.
                responseMessage = await httpClient.SendAsync(requestMessage);

                //Check whether the request is successfull.
                if (responseMessage != null)
                {
                    responseBody = await responseMessage.Content.ReadAsStringAsync();
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        return responseBody;
                    }
                    else if (responseMessage.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        //Throw exception in case of authorization denied
                        InvitationsMemoryCache.GetInstance().RemoveFromMemoryCache(bearerToken);
                        throw new Exception(SharedSettings.AuthorizationDenied);
                    }
                    else
                    {
                        if (_EventLogList != null)
                        {
                            _EventLogList.AddEventByLevel(2, $"StatusCode: {responseMessage.StatusCode} " +
                                $"ResponseMessage: {responseBody} Url: {url}", _batchID);
                        }
                        return null;
                    }
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                if (_EventLogList != null)
                {
                    _EventLogList.AddExceptionEvent(ex, null, null, null, null, "HTTP Send failed for HTTPWrapper sendAsync");
                    return null;
                }
                else
                {
                    throw ex;
                }
            }
        }

    }
}
