using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Invitations.Net
{
    public class AuthTokenValidation
    {
        private readonly ViaMongoDB ViaMongoDB;
        private readonly object authTokenLock = new object();

        public AuthTokenValidation(ViaMongoDB viaMongoDB)
        {
            ViaMongoDB = viaMongoDB;
        }

        public bool ValidateBearerToken(string authToken, AccountConfiguration accConfiguration)
        {
            try
            {
                if (accConfiguration == null)
                    return false;

                if (string.IsNullOrWhiteSpace(authToken))
                    return false;
                

                if (!(authToken.StartsWith("Bearer ") || authToken.StartsWith("Basic ")))
                    return false;

                if (InvitationsMemoryCache.GetInstance().GetFromMemoryCache(authToken) != null)
                    return true;
                else
                {
                    lock (authTokenLock) {

                        if (InvitationsMemoryCache.GetInstance().GetFromMemoryCache(authToken) != null)
                            return true;

                        var settings = new HTTPWrapper().GetSettings(authToken).GetAwaiter().GetResult();
                        if (string.IsNullOrWhiteSpace(settings))
                            return false;

                        Settings settingsRes = JsonConvert.DeserializeObject<Settings>(settings);

                        if (!settingsRes.user.Equals(accConfiguration?.WXMAdminUser, StringComparison.OrdinalIgnoreCase))
                            return false;

                        InvitationsMemoryCache.GetInstance().SetToMemoryCache("settings", settings);
                        InvitationsMemoryCache.GetInstance().SetAuthTokenToMemoryCache(authToken);
                    }
                    return true;
                }

            }
            catch (Exception)
            {
                return false;
            }

        }


        public async Task<bool> ValidateBearerToken(string authToken)
        {
            try
            {
                // Fetch AccountConfiguration stored in DB to validate the user
                AccountConfiguration accountConfiguration;
                var accountConfigurationCache = InvitationsMemoryCache.GetInstance().GetFromMemoryCache("accountconfig");
                if (accountConfigurationCache == null)
                {
                    accountConfiguration = await ViaMongoDB.GetAccountConfiguration();
                    InvitationsMemoryCache.GetInstance().SetToMemoryCache("accountconfig", JsonConvert.SerializeObject(accountConfiguration));
                }
                else
                {
                    accountConfiguration = JsonConvert.DeserializeObject<AccountConfiguration>(accountConfigurationCache);
                }

                return ValidateBearerToken(authToken, accountConfiguration);

            }
            catch (Exception)
            {
                return false;
            }

        }
    }
}
