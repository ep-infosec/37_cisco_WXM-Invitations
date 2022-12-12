using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Invitations.Net
{
    public class InvitationsMemoryCache
    {
        private readonly object dispatchLock = new object();
        private readonly object dpLock = new object();
        private readonly object questionsLock = new object();
        private readonly object settingsLock = new object();
        private readonly object questionniareLock = new object();
        private readonly object userprofileLock = new object();
        private readonly object templateLock = new object();

        private readonly MemoryCache Cache = new MemoryCache(new MemoryCacheOptions
        {
        });

        private readonly MemoryCacheEntryOptions cacheEntryOptionsCache = new MemoryCacheEntryOptions()
                    // Keep in cache for this time, reset time if accessed.
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(
                        (SharedSettings.CacheExpiryInSeconds == 0) ? 3600 : SharedSettings.CacheExpiryInSeconds
                        ));

        private readonly MemoryCacheEntryOptions cacheEntryOptionsAuthToken = new MemoryCacheEntryOptions()
                    // Keep in cache for this time, reset time if accessed.
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(
                        (SharedSettings.AuthTokenCacheExpiryInSeconds == 0) ? 900 : SharedSettings.AuthTokenCacheExpiryInSeconds
                        ));

        private readonly MemoryCacheEntryOptions bulkTokenAuth = new MemoryCacheEntryOptions();

        private static InvitationsMemoryCache _instance = new InvitationsMemoryCache();

        public void SetToMemoryCache(string key, string value)
        {
            // Save data in cache.
            Cache.Set(key, value, cacheEntryOptionsCache);
        }

        public void SetAuthTokenToMemoryCache(string authToken)
        {
            // Save data in cache.
            Cache.Set(authToken, "true", cacheEntryOptionsAuthToken);
        }

        public void SetBulkTokenAuthToMemoryCache(string key, string value, double seconds)
        {
            bulkTokenAuth.SetAbsoluteExpiration(TimeSpan.FromSeconds(seconds));
            Cache.Set(key, value, bulkTokenAuth);
        }

        public void SetToMemoryCacheSliding(string key, string value)
        {
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                    // Keep in cache for this time, reset time if accessed.
                    .SetSlidingExpiration(TimeSpan.FromSeconds(
                        (SharedSettings.CacheExpiryInSeconds == 0) ? 3600 : SharedSettings.CacheExpiryInSeconds
                        ));

            // Save data in cache.
            Cache.Set(key, value, cacheEntryOptions);
        }


        public string GetDispatchDataFromMemoryCache(string authToken, HTTPWrapper hTTPWrapper)
        {
            if (Cache.TryGetValue("DispatchData", out string value))
                return value;
            else
            {
                string DispatchData;
                lock (dispatchLock)
                {
                    if (Cache.TryGetValue("DispatchData", out string newValue))
                        return newValue;

                    DispatchData = hTTPWrapper.GetAllDispatchInfo(authToken).GetAwaiter().GetResult();
                    if (string.IsNullOrEmpty(DispatchData))
                    {
                        return null;
                    }

                    SetToMemoryCache("DispatchData", DispatchData);
                }
                return DispatchData;
            }
        }

        public string GetDeliveryPlanFromMemoryCache(string authToken, HTTPWrapper hTTPWrapper)
        {
            if (Cache.TryGetValue("DeliveryPlanData", out string value))
                return value;
            else
            {
                string DeliveryPlanData;
                lock (dpLock)
                {
                    if (Cache.TryGetValue("DeliveryPlanData", out string newValue))
                        return newValue;

                    DeliveryPlanData = hTTPWrapper.GetDeliveryPlans(authToken).GetAwaiter().GetResult();
                    if (string.IsNullOrEmpty(DeliveryPlanData))
                    {
                        return null;
                    }

                    SetToMemoryCache("DeliveryPlanData", DeliveryPlanData);
                }
                return DeliveryPlanData;
            }
        }

        public string GetDispatchDataForConfigFromMemoryCache(string authToken, WXMService wXMService)
        {
            string dispatches = string.Empty;
            if (Cache.TryGetValue("DispatchData", out string value))
                return value;
            else
            {
                lock (dispatchLock)
                {
                    if (Cache.TryGetValue("DispatchData", out string newValue))
                        return newValue;

                    var dispatchdata = wXMService.GetDispatches(authToken).GetAwaiter().GetResult();
                    dispatches = Newtonsoft.Json.JsonConvert.SerializeObject(dispatchdata);
                    if (string.IsNullOrEmpty(dispatches))
                    {
                        return null;
                    }

                    SetToMemoryCache("DispatchData", dispatches);
                }
                return dispatches;
            }
        }

        public string GetActiveQuestionsFromMemoryCache(string authToken, HTTPWrapper hTTPWrapper)
        {
            if (Cache.TryGetValue("ActiveQuestions", out string value))
                return value;
            else
            {
                string ActiveQuestions;
                lock (questionsLock)
                {
                    if (Cache.TryGetValue("ActiveQuestions", out string newValue))
                        return newValue;

                    ActiveQuestions = hTTPWrapper.GetActiveQuestions(authToken).GetAwaiter().GetResult();
                    if (string.IsNullOrEmpty(ActiveQuestions))
                    {
                        return null;
                    }

                    SetToMemoryCache("ActiveQuestions", ActiveQuestions);
                }
                return ActiveQuestions;
            }
        }


        public string GetSettingsFromMemoryCache(string authToken, HTTPWrapper hTTPWrapper)
        {
            if (Cache.TryGetValue("settings", out string value))
                return value;
            else
            {
                string Settings;
                lock (settingsLock)
                {
                    if (Cache.TryGetValue("settings", out string newValue))
                        return newValue;

                    Settings = hTTPWrapper.GetSettings(authToken).GetAwaiter().GetResult();
                    if (string.IsNullOrEmpty(Settings))
                    {
                        return null;
                    }

                    SetToMemoryCache("settings", Settings);
                }
                return Settings;
            }
        }

        public string GetQuestionnaireFromMemoryCache(string authToken, HTTPWrapper hTTPWrapper)
        {
            if (Cache.TryGetValue("SurveyQuestionnaires", out string value))
                return value;
            else
            {
                string SurveyQuestionnaires;
                lock (questionniareLock)
                {
                    if (Cache.TryGetValue("SurveyQuestionnaires", out string newValue))
                        return newValue;

                    SurveyQuestionnaires = hTTPWrapper.GetSurveyQuestionnaire(authToken).GetAwaiter().GetResult();
                    if (string.IsNullOrEmpty(SurveyQuestionnaires))
                    {
                        return null;
                    }

                    SetToMemoryCache("SurveyQuestionnaires", SurveyQuestionnaires);
                }
                return SurveyQuestionnaires;
            }
        }

        public string GetUserProfileFromMemoryCache(string authToken, HTTPWrapper hTTPWrapper)
        {
            if (Cache.TryGetValue("UserProfile", out string value))
                return value;
            else
            {
                string UserProfile;
                lock (userprofileLock)
                {
                    if (Cache.TryGetValue("UserProfile", out string newValue))
                        return newValue;

                    UserProfile = hTTPWrapper.GetUserProfile(authToken).GetAwaiter().GetResult();
                    if (string.IsNullOrEmpty(UserProfile))
                    {
                        return null;
                    }

                    SetToMemoryCache("UserProfile", UserProfile);
                }
                return UserProfile;
            }
        }

        public string GetContentTemplatesFromMemoryCache(string authToken, HTTPWrapper hTTPWrapper)
        {
            if (Cache.TryGetValue("ContentTemplates", out string value))
                return value;
            else
            {
                string ContentTemplates;
                lock (templateLock)
                {
                    if (Cache.TryGetValue("ContentTemplates", out string newValue))
                        return newValue;

                    ContentTemplates = hTTPWrapper.GetContentTemplates(authToken).GetAwaiter().GetResult();
                    if (string.IsNullOrEmpty(ContentTemplates))
                    {
                        return null;
                    }

                    SetToMemoryCache("ContentTemplates", ContentTemplates);
                }
                return ContentTemplates;
            }
        }

        public string GetFromMemoryCache(string key)
        {
            if (Cache.TryGetValue(key, out string value))
            {
                return value;
            }
            return null;
        }

        public void RemoveFromMemoryCache(string key)
        {
            Cache.Remove(key);
        }

        public static InvitationsMemoryCache GetInstance()
        {
            return _instance;
        }
    }
}
