using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Invitations.Net
{
    public class ConfigService
    {
        private ViaMongoDB ViaMongoDB;
        private WXMService WXMService;


        public ConfigService(ViaMongoDB viaMongoDB, WXMService wXMService)
        {
            ViaMongoDB = viaMongoDB;
            WXMService = wXMService;
        }

        public async Task<bool> IsUserAdminValid(string wxmAdmin)
        {
            AccountConfiguration accountConfiguration = await ViaMongoDB.GetAccountConfiguration();
            string dbAdmin = accountConfiguration?.WXMAdminUser;
            if (string.IsNullOrWhiteSpace(dbAdmin))
                return true;
            else
                return wxmAdmin == dbAdmin;
        }

        public async Task<ACMLoginResponse> Login(SPALoginRequest request)
        {
            ACMLoginResponse response = new ACMLoginResponse();
            try
            {
                BearerToken bearerToken = await WXMService.GetLoginToken(request.Username, request.Password);
                if (bearerToken == default || !(await IsUserAdminValid(bearerToken.ManagedBy)))
                {
                    response.IsSuccessful = false;
                    response.Message = "Incorrect Username/Password";
                }
                else
                {
                    string APIKey = await WXMService.GetAPIKey("Bearer " + bearerToken.AccessToken);
                    if (APIKey == default)
                    {
                        response.IsSuccessful = false;
                        response.Message = "No API Key found at WXM";
                    }
                    else
                    {
                        var tryUpdate = await ViaMongoDB.AddOrUpdateAccountConfiguration_WXMFields(bearerToken.ManagedBy, APIKey, bearerToken.UserName, SharedSettings.BASE_URL,
                            bearerToken.PrimaryRole);
                        if (tryUpdate == default)
                        {
                            response.IsSuccessful = false;
                            response.Message = "Account Configuration Update Has Failed";
                        }
                        else
                        {
                            response.IsSuccessful = true;
                            response.Message = bearerToken.AccessToken;
                            // Time Zone offset. 
                            int timezoneOffset = 0;
                            // Get User profile.
                            Profile profile = await WXMService.GetProfile("Bearer " + bearerToken.AccessToken);
                            if (profile?.timeZoneOffset == null)
                            {
                                // Get User Settings.
                                Settings settings = await WXMService.GetSettings("Bearer " + bearerToken.AccessToken);
                                timezoneOffset = settings?.TimeZoneOffset != null ? settings.TimeZoneOffset : 0;
                            }
                            else
                                timezoneOffset = profile.timeZoneOffset.HasValue ? profile.timeZoneOffset.Value : 0;
                            var Expirationtime = bearerToken.ExpiresIn;
                            InvitationsMemoryCache.GetInstance().SetBulkTokenAuthToMemoryCache(bearerToken.AccessToken, timezoneOffset.ToString(), Expirationtime);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                response.IsSuccessful = false;
                if (ex.Message == WXMService.LoginAPIErrorTwoFactorSecureCode)
                    response.Message = WXMService.LoginAPIErrorTwoFactorSecureCode;
                else if (ex.Message == WXMService.InvalidOTP)
                    response.Message = WXMService.InvalidOTP;
                else if (ex.Message == WXMService.LoginAPIUserBlocked)
                    response.Message = WXMService.LoginAPIUserBlocked;
                else if (ex.Message == SharedSettings.AdminLoginError)
                    response.Message = SharedSettings.AdminLoginError;
                else
                    response.Message = "Internal Exception";
            }
            return response;
        }

        public async Task<ACMGenericResult<DispatchesAndQueueDetails>> GetDispatches(string bearerToken)
        {
            var result = new ACMGenericResult<DispatchesAndQueueDetails>();
            try
            {
                List<Dispatch> dispatches = await WXMService.GetDispatches(bearerToken);
                List<DeliveryPlan> deliveryPlans = await WXMService.GetDeliveryPlans(bearerToken) ?? new List<DeliveryPlan>();
                List<Question> preFillQuestions = (await WXMService.GetActiveQuestions(bearerToken)).Where(x => x.StaffFill == true
                || x.ApiFill == true)?.ToList() ?? new List<Question>();
                Settings settings = await WXMService.GetSettings(bearerToken);

                if (dispatches?.Count > 0 == false || settings == null)
                {
                    result.StatusCode = 400;
                    result.Value = null;
                }
                else
                {
                    var configuredDispatchChannels = await ConfigureDispatchChannels(dispatches, deliveryPlans, preFillQuestions);
                    var configuredQueue = await ConfigureQueueDetails(settings);
                    result.StatusCode = 200;
                    result.Value = new DispatchesAndQueueDetails
                    {
                        Dispatches = configuredDispatchChannels?
                        .Select(x => new KeyValuePair<string, string>(x.DispatchId, x.DispatchName))?
                        .ToList() ?? new List<KeyValuePair<string, string>>(),
                        Queue = configuredQueue
                    };
                }
            }
            catch (Exception)
            {
                result.StatusCode = 500;
                result.Value = null;
            }
            return result;
        }

        public async Task<List<DispatchChannel>> ConfigureDispatchChannels(List<Dispatch> dispatches, List<DeliveryPlan> deliveryPlans, List<Question> preFillQuestions)
        {
            List<DispatchChannel> dispatchChannels = new List<DispatchChannel>();
            foreach (Dispatch dispatch in dispatches)
            {
                DeliveryPlan deliveryPlan = deliveryPlans.Find(x => x.id == dispatch.DeliveryPlanId);
                List<StaticPrefill> staticPrefills = preFillQuestions
                    .Where(x => ((x.PerLocationOverride == null || x.PerLocationOverride.Count == 0) 
                    && x.StaffFill == true && (x.DisplayLocation?.Contains(dispatch.QuestionnaireName) ?? false)) 
                    || (x.PerLocationOverride?.Find(x => x.Location == dispatch.QuestionnaireName && x.StaffFill == true)) != null)?
                    .Select(x => new StaticPrefill { Note = x.Note, PrefillValue = null, QuestionId = x.Id })?
                    .ToList() ?? new List<StaticPrefill>();
                staticPrefills.AddRange(preFillQuestions
                    .Where(x => x.ApiFill == true)?
                    .Select(x => new StaticPrefill { Note = x.Note, PrefillValue = null, QuestionId = x.Id })?
                    .ToList() ?? new List<StaticPrefill>());
                DispatchChannel dispatchChannel = new DispatchChannel
                {
                    ChannelDetails = new ChannelDetails
                    {
                        Email = new Channel
                        {
                            IsValid = deliveryPlan?.schedule?.Any(x => x.onChannel?.StartsWith("email") ?? false) ?? false ? true : false,
                            Vendorname = null
                        },
                        Sms = new Channel
                        {
                            IsValid = deliveryPlan?.schedule?.Any(x => x.onChannel?.StartsWith("sms") ?? false) ?? false ? true : false,
                            Vendorname = null
                        }
                    },
                    DispatchId = dispatch.Id,
                    DispatchName = dispatch.Name + (dispatch.IsLive == true ? string.Empty : " [PAUSED]"),
                    StaticPrefills = staticPrefills,
                    Notify = new Notify
                    {
                        D = null,
                        E = null,
                        F = null,
                        I = null,
                        W = null
                    }
                };
                dispatchChannels.Add(dispatchChannel);
            }

            AccountConfiguration accountConfiguration = await ViaMongoDB.GetAccountConfiguration();
            if (accountConfiguration.DispatchChannels == null)
                accountConfiguration.DispatchChannels = dispatchChannels;
            else
            {
                foreach (DispatchChannel dc in dispatchChannels)
                {
                    int index1 = accountConfiguration.DispatchChannels.FindIndex(x => x.DispatchId == dc.DispatchId);
                    if (index1 == -1)                                                                                   //Add new dispatch channel                                                                                   
                        accountConfiguration.DispatchChannels.Add(dc);
                    else                                                                                                //Update existing dispatch channel
                    {
                        accountConfiguration.DispatchChannels[index1].ChannelDetails.Email.IsValid = dc.ChannelDetails.Email.IsValid;
                        accountConfiguration.DispatchChannels[index1].ChannelDetails.Sms.IsValid = dc.ChannelDetails.Sms.IsValid;

                        accountConfiguration.DispatchChannels[index1].DispatchName = dc.DispatchName;

                        foreach (StaticPrefill sp in dc.StaticPrefills)
                        {
                            int index2 = accountConfiguration.DispatchChannels[index1].StaticPrefills.FindIndex(x => x.QuestionId == sp.QuestionId);
                            if (index2 == -1)
                                accountConfiguration.DispatchChannels[index1].StaticPrefills.Add(sp);                   //Add new static prefill
                            else
                                accountConfiguration.DispatchChannels[index1].StaticPrefills[index2].Note = sp.Note;    //Update existing static prefill
                        }
                        accountConfiguration.DispatchChannels[index1].
                            StaticPrefills.RemoveAll(x => dc.StaticPrefills.All(y => y.QuestionId != x.QuestionId));    //Remove old static prefills

                        if (accountConfiguration.DispatchChannels[index1].Notify == default)
                            accountConfiguration.DispatchChannels[index1].Notify = dc.Notify;
                    }
                }
                accountConfiguration.DispatchChannels                                                                   //Remove old dispatch channels
                    .RemoveAll(x => dispatchChannels.All(y => y.DispatchId != x.DispatchId));
            }
            return (await ViaMongoDB.UpdateAccountConfiguration_DispatchChannels(accountConfiguration.DispatchChannels)).DispatchChannels;
        }

        public async Task<Queue> ConfigureQueueDetails(Settings settings)
        {
            string queueName = settings.Integrations?.QueueDetails?.ElementAt(0)?.QueueName;
            string queueConnectionString = settings.Integrations?.QueueDetails?.ElementAt(0)?.ConnectionString;
            string queueType = settings.Integrations?.QueueDetails?.ElementAt(0)?.Type;
            Queue queue = new Queue
            {
                QueueType = string.IsNullOrWhiteSpace(queueType) ? "Details unavailable. Please check this in Experience Management" : queueType,
                QueueConnectionString = string.IsNullOrWhiteSpace(queueConnectionString) ? "Details unavailable. Please check this in Experience Management" : string.IsNullOrWhiteSpace(queueName) ? queueConnectionString : queueName + "@" + queueConnectionString
            };
            return (await ViaMongoDB.UpdateAccountConfiguration_Queue(queue)).Queue;
        }

        public async Task<ACMGenericResult<DispatchChannel>> GetDispatchChannel(string dispatchId)
        {
            var result = new ACMGenericResult<DispatchChannel>();
            try
            {
                AccountConfiguration accountConfiguration = await ViaMongoDB.GetAccountConfiguration();
                if (accountConfiguration.DispatchChannels == null)
                {
                    result.StatusCode = 204;
                    result.Value = null;
                }
                else
                {
                    DispatchChannel dispatchChannel = accountConfiguration.DispatchChannels.Find(x => x.DispatchId == dispatchId);
                    if (dispatchChannel == default)
                    {
                        result.StatusCode = 204;
                        result.Value = null;
                    }
                    else
                    {
                        result.StatusCode = 200;
                        result.Value = dispatchChannel;
                    }
                }
            }
            catch (Exception)
            {
                result.StatusCode = 500;
                result.Value = null;
            }
            return result;
        }

        public async Task<ACMGenericResult<DispatchChannel>> AddOrUpdateDispatchChannel(DispatchChannel dispatchChannel)
        {
            var result = new ACMGenericResult<DispatchChannel>();
            try
            {
                AccountConfiguration accountConfiguration = await ViaMongoDB.GetAccountConfiguration();
                if (accountConfiguration.DispatchChannels == null)
                    accountConfiguration.DispatchChannels = new List<DispatchChannel> { dispatchChannel };
                else
                {
                    int index = accountConfiguration.DispatchChannels.FindIndex(x => x.DispatchId == dispatchChannel.DispatchId);
                    if (index == -1)
                        accountConfiguration.DispatchChannels.Add(dispatchChannel);
                    else
                        accountConfiguration.DispatchChannels[index] = ToClone(dispatchChannel);
                }
                result.StatusCode = 200;
                result.Value = (await ViaMongoDB.UpdateAccountConfiguration_DispatchChannels(accountConfiguration.DispatchChannels))
                    .DispatchChannels?.Find(x => x.DispatchId == dispatchChannel.DispatchId);
            }
            catch (Exception)
            {
                result.StatusCode = 500;
                result.Value = null;
            }
            return result;
        }

        public async Task<ACMGenericResult<Vendor>> GetVendor(string vendorName)
        {
            var result = new ACMGenericResult<Vendor>();
            try
            {
                AccountConfiguration accountConfiguration = await ViaMongoDB.GetAccountConfiguration();
                if (accountConfiguration.Vendors == null)
                {
                    result.StatusCode = 204;
                    result.Value = null;
                }
                else
                {
                    Vendor vendor = accountConfiguration.Vendors
                        .Find(x => string.Equals(x.VendorName, vendorName, StringComparison.InvariantCultureIgnoreCase));
                    if (vendor == default)
                    {
                        result.StatusCode = 204;
                        result.Value = null;
                    }
                    else
                    {
                        result.StatusCode = 200;
                        result.Value = vendor;
                    }
                }
            }
            catch (Exception)
            {
                result.StatusCode = 500;
                result.Value = null;
            }
            return result;
        }

        public async Task<ACMGenericResult<CustomSMTPSetting>> GetSmtpSetting()
        {
            var result = new ACMGenericResult<CustomSMTPSetting>();
            try
            {
                AccountConfiguration accountConfiguration = await ViaMongoDB.GetAccountConfiguration();
                if (accountConfiguration.CustomSMTPSetting == null)
                {
                    result.StatusCode = 204;
                    result.Value = null;
                }
                else
                {
                    CustomSMTPSetting customSMTPSettings = accountConfiguration.CustomSMTPSetting;
                    if (customSMTPSettings == default)
                    {
                        result.StatusCode = 204;
                        result.Value = null;
                    }
                    else
                    {
                        result.StatusCode = 200;
                        result.Value = customSMTPSettings;
                    }
                }
            }
            catch (Exception)
            {
                result.StatusCode = 500;
                result.Value = null;
            }
            return result;
        }

        public async Task<ACMGenericResult<string>> CheckSmtpSetting(string tomail)
        {
            var result = new ACMGenericResult<string>();
            try
            {
                AccountConfiguration accountConfiguration = await ViaMongoDB.GetAccountConfiguration();
                if (accountConfiguration.CustomSMTPSetting == null)
                {
                    result.StatusCode = 204;
                    result.Value = "Custom SMTP Setting not found";
                    return result;
                }
                else
                {
                    CustomSMTPSetting customSMTPSettings = accountConfiguration.CustomSMTPSetting;
                    if (string.IsNullOrEmpty(accountConfiguration.CustomSMTPSetting.SenderEmailAddress)  ||
                        string.IsNullOrEmpty(accountConfiguration.CustomSMTPSetting.Username) ||
                        string.IsNullOrEmpty(accountConfiguration.CustomSMTPSetting.Password) ||
                        string.IsNullOrEmpty(accountConfiguration.CustomSMTPSetting.Host) ||
                        string.IsNullOrEmpty(accountConfiguration.CustomSMTPSetting.SenderName) ||
                        string.IsNullOrEmpty(accountConfiguration.CustomSMTPSetting.Port) ||
                        string.IsNullOrEmpty(accountConfiguration.CustomSMTPSetting.EnableSsl))
                    {
                        result.StatusCode = 400;
                        result.Value = "Custom SMTP Settings are not configured correctly. Please make sure all the mandatory parameters for Custom SMTP " +
                            "are configured in ACM guide.";
                        return result;
                    }

                    var portStatus = int.TryParse(accountConfiguration.CustomSMTPSetting.Port, out int port);
                    if (!portStatus)
                        port = 587;
                    string username = accountConfiguration.CustomSMTPSetting.Username;
                    string password = accountConfiguration.CustomSMTPSetting.Password;
                    string host = accountConfiguration.CustomSMTPSetting.Host;
                    string from = accountConfiguration.CustomSMTPSetting.SenderEmailAddress;
                    string senderName = accountConfiguration.CustomSMTPSetting.SenderName;
                    bool.TryParse(accountConfiguration.CustomSMTPSetting.EnableSsl, out bool isSSLEnabled);
                    string subject = $"Test Email sent using current ACM Custom SMTP Settings";

                    MailMessage mail = new MailMessage
                    {
                        From = new MailAddress(from, senderName)
                    };
                    mail.To.Add(tomail);

                    SmtpClient client = new SmtpClient
                    {
                        Port = port,
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        Credentials = new NetworkCredential(username, password),
                        Host = host,
                        EnableSsl = isSSLEnabled
                    };
                    mail.Subject = subject;
                    try
                    {
                        client.Send(mail);
                    }
                    catch (Exception ex)
                    {
                        result.StatusCode = 400;
                        result.Value = ex.Message;
                        return result;
                    }

                    result.StatusCode = 200;
                    result.Value = "Test email processed. Please check if you received the email and the SMTP settings are correct.";
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = 500;
                result.Value = ex.Message;
            }
            return result;
        }

        public async Task<ACMGenericResult<EventLogObject>> GetEventLogs(ActivityFilter filterObject, string authToken)
        {
            var result = new ACMGenericResult<EventLogObject>();
            result.Value = new EventLogObject();
            CultureInfo provider = CultureInfo.InvariantCulture;
            try
            {
                if (!string.IsNullOrEmpty(filterObject.UUID) && string.IsNullOrEmpty(filterObject.Token))
                {
                    if (!DateTime.TryParseExact(filterObject.FromDate, "dd/MM/yyyy", provider, DateTimeStyles.None,
                out DateTime fromdate) || !DateTime.TryParseExact(filterObject.ToDate,
                 "dd/MM/yyyy", provider, DateTimeStyles.None, out DateTime todate))
                        throw new Exception("EventLog date filters are empty.");
                }
                List<EventLogs> value = new List<EventLogs>();
                List<LogEvent> logEvents = await ViaMongoDB.GetActivityDocuments(filterObject, true);
                // Time Zone offset. 
                int timezoneOffset;
                string timeSign = "+";
                string timeoffsetString = InvitationsMemoryCache.GetInstance().GetFromMemoryCache(authToken.Split(' ')[1]);
                if (string.IsNullOrEmpty(timeoffsetString))
                    timezoneOffset = 0;
                else
                    timezoneOffset = int.Parse(timeoffsetString);
                if (timezoneOffset < 0)
                    timeSign = "-";
                TimeSpan ts = TimeSpan.FromMinutes(timezoneOffset);
                if (logEvents != null && logEvents.Count != 0)
                {
                    var dispatches = GetDispatchData(authToken);
                    var target = logEvents[0];
                    var eventsBytarget = await GetDeliveryEventsByTaget(authToken, target.TargetHashed);
                    int maxReminder = 0;
                    foreach (var eventlog in logEvents)
                    {
                        EventLogs logs = new EventLogs();
                        List<EventReminderLog> eventReminderLog = new List<EventReminderLog>();
                        var eventbytarget = eventsBytarget?.Find(x => x.id == eventlog.TokenId
                        && x.deliveryWorkFlowId == eventlog.DeliveryWorkFlowId);
                        var dispatch = dispatches?.Find(x => x.Id == eventlog.DispatchId);
                        logs.TokenID = eventlog.TokenId;
                        logs.Dispatch = dispatch?.Name ?? string.Empty;
                        logs.DispatchID = eventlog.DispatchId;
                        string questionnaireName = dispatch?.QuestionnaireDisplayName ?? string.Empty;
                        logs.Questionnaire = questionnaireName;
                        logs.BatchId = eventlog.BatchId;
                        string channel = eventbytarget?.events.Count > 0 ? eventbytarget.events[0]?.channel?.Split("://")[0] : string.Empty;
                        logs.Channel = channel;
                        logs.UUID = eventlog.Target;
                        var recordEvents = eventlog.Events;
                        if (recordEvents.Find(x => x.Action == EventAction.Requested) != null)
                        {
                            logs.RecordStatus = "Accepted";
                            var tokenCreationEvent = recordEvents.Find(x => x.Action == EventAction.TokenCreated);
                            if (tokenCreationEvent != null)
                            {
                                logs.TokenCreationTime = tokenCreationEvent.TimeStamp.AddMinutes(timezoneOffset).ToString("dd/MM/yyyy hh:mm tt UTC ") + timeSign + ts.ToString(@"h\:mm");
                            }

                            var dpDispatchEvents = eventbytarget?.events;
                            if (dpDispatchEvents != null && dpDispatchEvents.Count != 0)
                            {
                                int reminderCount = 0;
                                var dispatchEvents = recordEvents.FindAll(x => x.Action == EventAction.DispatchUnsuccessful
                                || x.Action == EventAction.DispatchSuccessful);
                                var dpUnsEvent = dpDispatchEvents.FirstOrDefault(x => x.action == "Unsubscribe");
                                var dpDispatchSentEvent = dpDispatchEvents.FindAll(x => x.action == "Sent" || x.action == "Exception");
                                var dpDispatchLastSentEvent = dpDispatchEvents.LastOrDefault(x => x.action == "Sent");
                                var dpDispatchOtherEvent = dpDispatchEvents.LastOrDefault(x => x.action == "Throttled"
                                 || x.action == "Unsubscribed");
                                var dpAnsweredEvent = dpDispatchEvents.LastOrDefault(x => x.action == "Answered");
                                foreach (var dpEvent in dpDispatchSentEvent)
                                {
                                    string reminderCheck = "n=" + reminderCount.ToString();
                                    var dispatchEvent = dispatchEvents?.Find(x => x.Message.Contains(reminderCheck));
                                    var reminderChannel = dispatchEvent?.Message?.Split("&c=").Count() > 1 ? int.Parse(dispatchEvent?.Message.Split("&c=")[1].Substring(0, 1)) : -1;
                                    if (reminderCount == 0)
                                    {
                                        logs.DispatchVendor = dispatchEvent?.LogMessage?.Message?.Split("via: ")?.Count() > 1 ?
                                            dispatchEvent?.LogMessage?.Message?.Split("via: ")[1]?.Split(")")[0] : string.Empty;
                                        logs.DispatchStatus = dispatchEvent?.Action.ToString();
                                        logs.DispatchTime = dispatchEvent != null ? dispatchEvent?.TimeStamp.AddMinutes(timezoneOffset).ToString("dd/MM/yyyy hh:mm tt UTC ") + timeSign + ts.ToString(@"h\:mm") :
                                            string.Empty;
                                        if (dpDispatchLastSentEvent != null && dpDispatchLastSentEvent.Equals(dpEvent) &&
                                            (dpUnsEvent != null || dpAnsweredEvent != null))
                                        {
                                            if (dpUnsEvent != null && dpAnsweredEvent == null)
                                                logs.DPDispatchStatus = dpEvent.action + " : Unsubscribe";
                                            else if (dpUnsEvent == null && dpAnsweredEvent != null)
                                                logs.DPDispatchStatus = dpEvent.action + " : Answered";
                                            else
                                                logs.DPDispatchStatus = dpEvent.action + " : Answered : Unsubscribe";
                                        }
                                        else
                                            logs.DPDispatchStatus = dpEvent.action == "Sent" ? dpEvent.action : dpEvent.action + " - "
                                                + dpEvent.message;
                                        logs.DPDispatchTime = dpEvent.timeStamp.AddMinutes(timezoneOffset).ToString("dd/MM/yyyy hh:mm tt UTC ") + timeSign + ts.ToString(@"h\:mm");
                                        if (dispatchEvent?.Action == EventAction.DispatchUnsuccessful)
                                            logs.DispatchRejectReason = dispatchEvent?.LogMessage?.Message ?? string.Empty;
                                    }
                                    //var reminderNumber = dpEvent.Message?.Split("&n=").Count() > 1 ? dpEvent.Message?.Split("&n=")[1] : "-1";
                                    else if (reminderCount > 0)
                                    {
                                        var dpStatus = dpEvent.action;
                                        string dpStatusValue = string.Empty;
                                        if (dpDispatchLastSentEvent != null && dpDispatchLastSentEvent.Equals(dpEvent) &&
                                            (dpUnsEvent != null || dpAnsweredEvent != null))
                                        {
                                            if (dpUnsEvent != null && dpAnsweredEvent == null)
                                                dpStatusValue = dpEvent.action + " : Unsubscribe";
                                            else if (dpUnsEvent == null && dpAnsweredEvent != null)
                                                dpStatusValue = dpEvent.action + " : Answered";
                                            else
                                                dpStatusValue = dpEvent.action + " : Answered : Unsubscribe";
                                        }
                                        else
                                            dpStatusValue = dpEvent.action;
                                        EventReminderLog eventReminder = new EventReminderLog
                                        {
                                            Channel = (dpEvent.channel != null && dpEvent.channel.Contains("://"))
                                            ? dpEvent.channel?.Split("://")[0] : string.Empty,
                                            ReminderNumber = reminderCount,
                                            ReminderTime = dispatchEvent != null ? dispatchEvent?.TimeStamp.AddMinutes(timezoneOffset).ToString("dd/MM/yyyy hh:mm tt UTC ") + timeSign + ts.ToString(@"h\:mm") :
                                            string.Empty,
                                            ReminderDPStatus = dpStatus == "Sent" ? dpStatusValue : dpStatusValue + " - " + dpEvent.message,
                                            ReminderDispatchStatus = dispatchEvent != null ? dispatchEvent?.Action == EventAction.DispatchSuccessful
                                            ? dispatchEvent?.Action.ToString() : dispatchEvent?.Action.ToString() + " - " + dispatchEvent?.LogMessage?.Message : string.Empty
                                        };
                                        eventReminderLog.Add(eventReminder);
                                    }
                                    reminderCount++;
                                }
                                if (dpDispatchOtherEvent != null)
                                {
                                    if (reminderCount == 0)
                                    {
                                        var dpStatus = string.Empty;
                                        if (!string.IsNullOrEmpty(dpDispatchOtherEvent?.message))
                                            dpStatus = dpDispatchOtherEvent?.action + " - " + dpDispatchOtherEvent?.message;
                                        else
                                            dpStatus = dpDispatchOtherEvent.action;
                                        logs.DispatchVendor = string.Empty;
                                        logs.DispatchStatus = string.Empty;
                                        logs.DispatchTime = string.Empty;
                                        logs.DPDispatchStatus = dpStatus;
                                        logs.DPDispatchTime = dpDispatchOtherEvent.timeStamp.AddMinutes(timezoneOffset).ToString("dd/MM/yyyy hh:mm tt UTC ") + timeSign + ts.ToString(@"h\:mm");
                                    }
                                    //var reminderNumber = dpEvent.Message?.Split("&n=").Count() > 1 ? dpEvent.Message?.Split("&n=")[1] : "-1";
                                    else if (reminderCount > 0)
                                    {
                                        var reminderDPStatus = string.Empty;
                                        if (!string.IsNullOrEmpty(dpDispatchOtherEvent?.message))
                                            reminderDPStatus = dpDispatchOtherEvent.action + " - " + dpDispatchOtherEvent.message;
                                        else
                                            reminderDPStatus = dpDispatchOtherEvent.action;
                                        EventReminderLog eventReminder = new EventReminderLog
                                        {
                                            Channel = (dpDispatchOtherEvent.channel != null && dpDispatchOtherEvent.channel.Contains("://"))
                                             ? dpDispatchOtherEvent.channel?.Split("://")[0] : string.Empty,
                                            ReminderNumber = reminderCount,
                                            ReminderTime = string.Empty,
                                            ReminderDPStatus = reminderDPStatus,
                                            ReminderDispatchStatus = string.Empty
                                        };
                                        eventReminderLog.Add(eventReminder);
                                    }
                                }
                                maxReminder = Math.Max(maxReminder, eventReminderLog.Count());
                            }
                        }
                        else
                        {
                            logs.RecordStatus = "Rejected";
                            //Reject reason will also come.
                            var rejectEvent = recordEvents.Find(x => x.Action == EventAction.Rejected ||
                            x.Action == EventAction.Throttled);
                            if (rejectEvent != null)
                            {
                                logs.RecordRejectReason = rejectEvent.LogMessage?.Message ?? string.Empty;
                            }
                        }
                        logs.Reminder = eventReminderLog;
                        value.Add(logs);
                    }
                    result.StatusCode = 200;
                    result.Value.NumberofRows = value.Count();
                    result.Value.MaxReminders = maxReminder;
                    result.Value.EventLogs = value;
                }
                else
                {
                    result.StatusCode = 200;
                    result.Value = null;
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }
            return result;
        }

        public List<Dispatch> GetDispatchData(string bearerToken)
        {
            try
            {
                string dispatchdata = InvitationsMemoryCache.GetInstance().GetDispatchDataForConfigFromMemoryCache(bearerToken, WXMService);
                if (string.IsNullOrEmpty(dispatchdata))
                    return null;
                return JsonConvert.DeserializeObject<List<Dispatch>>(dispatchdata);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<DeliveryEventsByTarget>> GetDeliveryEventsByTaget(string auth_token, string target)
        {
            try
            {
                List<DeliveryEventsByTarget> deliveryEventsByTargets = await WXMService.GetDeliveryEventsBy(auth_token, target);
                return deliveryEventsByTargets;

            }
            catch (Exception)
            {
                return null;
            }

        }

        public async Task<ACMGenericResult<Vendor>> AddOrUpdateVendor(Vendor newVendor)
        {
            var result = new ACMGenericResult<Vendor>();
            try
            {
                AccountConfiguration accountConfiguration = await ViaMongoDB.GetAccountConfiguration();
                if (accountConfiguration.Vendors == null)
                    accountConfiguration.Vendors = new List<Vendor> { newVendor };
                else
                {
                    int index = accountConfiguration.Vendors.FindIndex(x => string.Equals(x.VendorName, newVendor.VendorName));
                    if (index == -1)
                        accountConfiguration.Vendors.Add(newVendor);
                    else
                        accountConfiguration.Vendors[index] = ToClone(newVendor);
                }
                result.StatusCode = 200;
                result.Value = (await ViaMongoDB.UpdateAccountConfiguration_Vendors(accountConfiguration.Vendors))
                    .Vendors.Find(x => string.Equals(x.VendorName, newVendor.VendorName));
            }
            catch (Exception)
            {
                result.StatusCode = 500;
                result.Value = null;
            }
            return result;
        }

        public async Task<ACMGenericResult<CustomSMTPSetting>> AddOrUpdateSMTPSetting(CustomSMTPSetting customSMTPSettings)
        {
            var result = new ACMGenericResult<CustomSMTPSetting>();
            try
            {
                if (string.IsNullOrEmpty(customSMTPSettings.SenderEmailAddress) ||
                        string.IsNullOrEmpty(customSMTPSettings.Username) ||
                        string.IsNullOrEmpty(customSMTPSettings.Password) ||
                        string.IsNullOrEmpty(customSMTPSettings.Host) ||
                        string.IsNullOrEmpty(customSMTPSettings.SenderName) ||
                        string.IsNullOrEmpty(customSMTPSettings.Port) ||
                        string.IsNullOrEmpty(customSMTPSettings.EnableSsl))
                {
                    result.StatusCode = 400;
                    result.Value = null;
                    return result;
                }
                AccountConfiguration accountConfiguration = await ViaMongoDB.GetAccountConfiguration();
                accountConfiguration.CustomSMTPSetting = customSMTPSettings;
                result.StatusCode = 200;
                await ViaMongoDB.UpdateAccountConfiguration_SMTPSetting(customSMTPSettings);
                result.Value = customSMTPSettings;
            }
            catch (Exception)
            {
                result.StatusCode = 500;
                result.Value = null;
            }
            return result;
        }

        public async Task<ACMGenericResult<Dictionary<string, string>>> GetExtendedProperties()
        {
            var result = new ACMGenericResult<Dictionary<string, string>>();
            try
            {
                result.StatusCode = 200;
                result.Value = (await ViaMongoDB.GetAccountConfiguration()).ExtendedProperties;
            }
            catch (Exception)
            {
                result.StatusCode = 500;
                result.Value = null;
            }
            return result;
        }

        public async Task<ACMGenericResult<Dictionary<string, string>>> UpdateExtendedProperties(Dictionary<string, string> extendedProperties)
        {
            var result = new ACMGenericResult<Dictionary<string, string>>();
            try
            {
                result.StatusCode = 200;
                if (!extendedProperties.ContainsKey("CheckCleanData"))
                    extendedProperties.Add("CheckCleanData", "true");
                result.Value = (await ViaMongoDB.UpdateAccountConfiguration_ExtendedProperties(extendedProperties)).ExtendedProperties;
            }
            catch (Exception)
            {
                result.StatusCode = 500;
                result.Value = null;
            }
            return result;
        }

        public async Task<ACMGenericResult<string>> DeleteAccountConfiguration()
        {
            var deleteResponseObj = new ACMGenericResult<string>();
            try
            {
                await ViaMongoDB.DeleteAccountConfiguration();
                deleteResponseObj.StatusCode = 200;
                deleteResponseObj.Value = "Account has been cleared";
            }
            catch (Exception)
            {
                deleteResponseObj.StatusCode = 500;
                deleteResponseObj.Value = null;
            }
            return deleteResponseObj;
        }

        public T ToClone<T>(T obj)
        {
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj));
        }
    }
}
