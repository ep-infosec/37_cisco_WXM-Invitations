using DispatcherEmailService.Cache;
using DispatcherEmailService.Helper;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Text;
using XM.ID.Net;

namespace DispatcherEmailService.Middleware
{
    public class BasicAuthenticationMiddleware
    {
        private readonly IConfiguration _configuration;
        private readonly ConfigurationCache _configurationCache;

        public BasicAuthenticationMiddleware(IConfiguration configuration, ConfigurationCache configurationCache)
        {
            _configuration = configuration;
            _configurationCache = configurationCache;
        }

        public bool Authenticate(string authorization, RequestBody requestBody)
        {
            try
            {
                if (authorization != null && authorization.StartsWith("Basic"))
                {
                    //Extract credentials
                    string encodedString = authorization["Basic ".Length..].Trim();
                    Encoding encoding = Encoding.GetEncoding("ISO-8859-1");
                    string userAndPassword = encoding.GetString(Convert.FromBase64String(encodedString));

                    string confString = _configurationCache.GetConfigurationDataFromCache();

                    if (string.IsNullOrEmpty(confString))
                    {
                        return false;
                    }

                    AccountConfiguration accountConfiguration = JsonConvert.DeserializeObject<AccountConfiguration>(confString);

                    string hashedPassword = SHAHashing.GetSHA256Hash(accountConfiguration.WXMAPIKey);

                    int index = userAndPassword.IndexOf(':');

                    var username = userAndPassword.Substring(0, index);
                    var password = userAndPassword[(index + 1)..];

                    if (username == accountConfiguration.WXMAdminUser && password == hashedPassword)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
