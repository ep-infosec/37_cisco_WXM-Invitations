using DispatcherEmailService.Helper;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace DispatcherEmailService.Cache
{
    public class ConfigurationCache
    {
        private readonly object configurationLock = new object();
        private readonly ViaMongoDB _viaMongoDB;

        private readonly MemoryCache Cache = new MemoryCache(new MemoryCacheOptions
        {
        });

        public ConfigurationCache(ViaMongoDB viaMongoDB)
        {
            _viaMongoDB = viaMongoDB;
        }

        public void SetToMemoryCache(string key, string value)
        {
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                    // Keep in cache for this time, reset time if accessed.
                    .SetSlidingExpiration(TimeSpan.FromSeconds(3600));

            // Save data in cache.
            Cache.Set(key, value, cacheEntryOptions);
        }

        public string GetConfigurationDataFromCache()
        {
            if (Cache.TryGetValue("ConfigurationData", out string value))
                return value;
            else
            {
                string ConfigurationData;
                lock (configurationLock)
                {
                    if (Cache.TryGetValue("ConfigurationData", out string newValue))
                        return newValue;

                    ConfigurationData = _viaMongoDB.GetAccountConfiguration().GetAwaiter().GetResult();
                    if (string.IsNullOrEmpty(ConfigurationData))
                    {
                        return null;
                    }

                    SetToMemoryCache("ConfigurationData", ConfigurationData);
                }
                return ConfigurationData;
            }
        }
    }
}
