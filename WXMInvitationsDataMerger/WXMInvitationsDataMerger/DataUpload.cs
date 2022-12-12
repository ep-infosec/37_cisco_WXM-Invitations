using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XM.ID.Invitations.Net;
using XM.ID.Net;

namespace WXMInvitationsDataMerger
{
    class DataUpload
    {
        ApplicationLog log;
        IConfigurationRoot Configuration;
        SMTPServer smtpServer;
        readonly HTTPWrapper hTTPWrapper;
        ViaMongoDB via;

        public DataUpload(IConfigurationRoot configuration, ApplicationLog applog, ViaMongoDB v)
        {
            Configuration = configuration;
            log = applog;
            hTTPWrapper = new HTTPWrapper();
            via = v;
        }

        public async Task DataUploadTask(int SortOrder)
        {
            log.logMessage += $"Started on : {DateTime.UtcNow.ToString()} ";

            double.TryParse(Configuration["DataUploadSettings:UploadDataForLastHours"], out double LastHours);

            if (LastHours == 0)
            {
                log.logMessage += " LastHours needs to be a number";
                log.AddLogsToFile(DateTime.UtcNow);
                return;
            }

            await RunDataUploadTask(LastHours, SortOrder);
        }

        public async Task RunDataUploadTask(double LastHours, int SortOrder)
        {
            try
            {
                FilterBy filter = new FilterBy() { afterdate = DateTime.UtcNow.AddHours(-LastHours), beforedate = DateTime.UtcNow };

                AccountConfiguration a = await via.GetAccountConfiguration();

                string bearer = null;

                string responseBody = await hTTPWrapper.GetLoginToken(a.WXMAdminUser, a.WXMAPIKey);
                if (!string.IsNullOrEmpty(responseBody))
                {
                    BearerToken loginToken = Newtonsoft.Json.JsonConvert.DeserializeObject<BearerToken>(responseBody);
                    bearer = loginToken.AccessToken;
                }

                List<Question> questions = null;

                string q = InvitationsMemoryCache.GetInstance().GetActiveQuestionsFromMemoryCache("Bearer "+ bearer, hTTPWrapper);
                if (!string.IsNullOrEmpty(q))
                    questions = JsonConvert.DeserializeObject<List<Question>>(q);

                long Total = await via.GetEventsCount(filter);

                log.logMessage += $" Total documents to update- " + Total.ToString() + "\n\n";

                //take 10000 tokens at a time to not overload memory
                for (int i = 0; i < Total; i = i + 10000)
                {
                    if (i + 10000 >= Total)
                    {
                        log.logMessage += $" Batch starting at - " + DateTime.UtcNow.ToString() + " documents - " + i.ToString() + " - " + (i + 20000).ToString() + "\n\n";

                        List<WXMPartnerMerged> FinalData = await via.GetMergedData(filter, bearer, questions, i, 20000, SortOrder); // limitting to 20k in this case since we just take the rest of the records

                        if (FinalData != null)
                            await via.Upload(FinalData);

                        continue;
                    }

                    log.logMessage += $" Merging of WXM and partner data starting at - " + DateTime.UtcNow.ToString() + " documents - " + i.ToString() + " - " + (i + 10000).ToString() + "\n\n";

                    List<WXMPartnerMerged> data = await via.GetMergedData(filter, bearer, questions, i, 10000, SortOrder);

                    log.logMessage += $" Merging of WXM and partner data ending at - " + DateTime.UtcNow.ToString() + " documents - " + i.ToString() + " - " + (i + 10000).ToString() + "\n\n";

                    if (data != null)
                    {
                        log.logMessage += $" Merged data upload starting at - " + DateTime.UtcNow.ToString() + " documents - " + i.ToString() + " - " + (i + 10000).ToString() + "\n\n";
                        await via.Upload(data);
                        log.logMessage += $" Merged data upload ending at - " + DateTime.UtcNow.ToString() + " documents - " + i.ToString() + " - " + (i + 10000).ToString() + "\n\n";
                    }
                }
            }
            catch(Exception ex)
            {
                log.logMessage += $"Error uploading the data {ex.Message}    {ex.StackTrace}";
                return;
            }
        }
    }
}
