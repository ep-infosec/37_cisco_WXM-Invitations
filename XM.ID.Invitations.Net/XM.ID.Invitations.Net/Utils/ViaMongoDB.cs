using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Invitations.Net
{
    public class ViaMongoDB
    {
        readonly IMongoCollection<AccountConfiguration> _AccountConfiguration;
        readonly IMongoCollection<Unsubscribed> _Unsubscribe;
        readonly IMongoCollection<LogEvent> _EventLog;
        readonly IMongoCollection<DB_MessagePayload> _BulkMessage;
        readonly int _maximumLevel;
        IMongoCollection<WXMPartnerMerged> _mergedData;
        IMongoCollection<OnDemandReportModel> _OnDemand;
        IMongoCollection<RequestInitiatorRecords> _RequestInitiator;
        IConfiguration Configuration;
        readonly HTTPWrapper hTTPWrapper;

        public ViaMongoDB(IConfiguration configuration)
        {
            Configuration = configuration;

            MongoClientSettings settings = MongoClientSettings.FromUrl(new MongoUrl(configuration["MONGODB_URL"]));
            settings.MaxConnectionIdleTime = TimeSpan.FromMinutes(3);
            settings.ConnectTimeout = TimeSpan.FromSeconds(20);
            settings.MaxConnectionPoolSize = 1000;
            settings.ReadPreference = ReadPreference.Nearest;
            var mongoClient = new MongoClient(settings);
            IMongoDatabase asyncdb = mongoClient.GetDatabase(configuration["DbNAME"]);
            int.TryParse(configuration["LoggingMaximumLevel"], out _maximumLevel);

            _AccountConfiguration = asyncdb.GetCollection<AccountConfiguration>("AccountConfiguration");
            _Unsubscribe = asyncdb.GetCollection<Unsubscribed>("Unsubscribe");
            _EventLog = asyncdb.GetCollection<LogEvent>("EventLog");
            _BulkMessage = asyncdb.GetCollection<DB_MessagePayload>("BulkMessage");
            _mergedData = asyncdb.GetCollection<WXMPartnerMerged>("MergedData");
            _OnDemand = asyncdb.GetCollection<OnDemandReportModel>("OnDemandRequest");
            _RequestInitiator = asyncdb.GetCollection<RequestInitiatorRecords>("RequestInitiatorRecords");

            hTTPWrapper = new HTTPWrapper();

            #region Create Index
#pragma warning disable CS0618
            // For Dispatcher module to lookup and push the log events
            _EventLog.Indexes.CreateOneAsync(Builders<LogEvent>.IndexKeys.Ascending(x => x.TokenId)
.Ascending(x => x.BatchId).Ascending(x => x.DispatchId), new CreateIndexOptions { Background = false });


            // For updating the logevent once token is created
            _EventLog.Indexes.CreateOneAsync(Builders<LogEvent>.IndexKeys.Ascending(x => x.BatchId)
.Ascending(x => x.TargetHashed).Ascending(x => x.DispatchId), new CreateIndexOptions { Background = false });

            _EventLog.Indexes.CreateOneAsync(Builders<LogEvent>.IndexKeys.Ascending(x => x.Target),
                new CreateIndexOptions { Background = false });

            // For auto expiring the records after 3 months i.e. 7890000  seconds
            // Applied on Created At field
            _EventLog.Indexes.CreateOneAsync(Builders<LogEvent>.IndexKeys.Ascending(x => x.Created),
                new CreateIndexOptions {
                    Background = false,
                    ExpireAfter = TimeSpan.FromSeconds(int.Parse(configuration["MONGODB_RECORDS_EXPIRY"]))
                });

            //for filtering out the merged data
            _mergedData.Indexes.CreateOneAsync(Builders<WXMPartnerMerged>.IndexKeys.Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Background = false });

            //expire merged data after 3 months
            _mergedData.Indexes.CreateOneAsync(Builders<WXMPartnerMerged>.IndexKeys.Ascending(x => x.CreatedAt),
                new CreateIndexOptions
                {
                    Background = false,
                    ExpireAfter = TimeSpan.FromSeconds(int.Parse(configuration["MONGODB_RECORDS_EXPIRY"]))
                });

            //For Time-Trigger Serverless Computes
            _BulkMessage.Indexes.CreateOneAsync(Builders<DB_MessagePayload>.IndexKeys.Ascending(x => x.BulkVendorName)
                .Ascending(x => x.Status).Ascending(x => x.InsertTime), new CreateIndexOptions { Background = false });

            // For Getting Initiator records with filename
            _RequestInitiator.Indexes.CreateOneAsync(Builders<RequestInitiatorRecords>.IndexKeys.Ascending(x => x.FileName)
                , new CreateIndexOptions { Background = false });

#pragma warning restore CS0618
            #endregion
        }

        public async Task<List<LogEvent>> GetActivityDocuments(ActivityFilter filterObject, bool excludeNullEvents = false)
        {
            var filter = Builders<LogEvent>.Filter.Empty;
            CultureInfo provider = CultureInfo.InvariantCulture;
            if (!string.IsNullOrWhiteSpace(filterObject.Token))
                filter &= Builders<LogEvent>.Filter.Eq(x => x.TokenId, filterObject.Token);
            if (!string.IsNullOrWhiteSpace(filterObject.BatchId))
                filter &= Builders<LogEvent>.Filter.Eq(x => x.BatchId, filterObject.BatchId);
            if (!string.IsNullOrWhiteSpace(filterObject.DispatchId))
                filter &= Builders<LogEvent>.Filter.Eq(x => x.DispatchId, filterObject.DispatchId);
            if (!string.IsNullOrWhiteSpace(filterObject.UUID))
                filter &= Builders<LogEvent>.Filter.Eq(x => x.Target, filterObject.UUID);
            if (DateTime.TryParseExact(filterObject.FromDate, "dd/MM/yyyy", provider, DateTimeStyles.None,
                out DateTime fromdate) && DateTime.TryParseExact(filterObject.ToDate,
                 "dd/MM/yyyy", provider, DateTimeStyles.None, out DateTime todate))
                filter &= Builders<LogEvent>.Filter.Gte(x => x.Created, fromdate.Date)
                    & Builders<LogEvent>.Filter.Lt(x => x.Created, todate.AddDays(1));
            if (excludeNullEvents)
                filter &= Builders<LogEvent>.Filter.Ne(x => x.Events, null);
            return await _EventLog.Find(filter).ToListAsync();
        }

        public async Task<AccountConfiguration> GetAccountConfiguration()
        {
            return await _AccountConfiguration.Find(_ => true).FirstOrDefaultAsync();
        }

        public async Task<AccountConfiguration> AddOrUpdateAccountConfiguration_WXMFields(string adminUser, string apiKey, string user, string baseUrl,
            string primaryRole)
        {
            AccountConfiguration account = await _AccountConfiguration.Find(x => true).FirstOrDefaultAsync();
            if ((account == null || string.IsNullOrEmpty(account.WXMAdminUser)) && (primaryRole != "User"))
            {
                throw new Exception(SharedSettings.AdminLoginError);
            }
            Dictionary<string, string> defaultExtendedProperties = new Dictionary<string, string>
            {
                { "BatchingQueue", "inmemory" },
                { "Sampler", "wxm" },
                { "Unsubscriber", "wxm" },
                { "AccountNotifications", "" }
            };
            var filter = Builders<AccountConfiguration>.Filter.Empty;
            var update = Builders<AccountConfiguration>.Update
                .SetOnInsert(x => x.DispatchChannels, null)
                .SetOnInsert(x => x.Id, ObjectId.GenerateNewId().ToString())
                .SetOnInsert(x => x.Vendors, null)
                .SetOnInsert(x => x.ExtendedProperties, defaultExtendedProperties)
                .SetOnInsert(x => x.WXMAPIKey, apiKey)
                .SetOnInsert(x => x.WXMUser, user)
                .Set(x => x.Queue, null)
                .Set(x => x.WXMAdminUser, adminUser)
                .Set(x => x.WXMBaseURL, baseUrl);
            var opts = new FindOneAndUpdateOptions<AccountConfiguration> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            return await _AccountConfiguration.FindOneAndUpdateAsync<AccountConfiguration>(filter, update, opts);
        }

        public async Task<AccountConfiguration> UpdateAccountConfiguration_DispatchChannels(List<DispatchChannel> dispatchChannels)
        {
            var filter = Builders<AccountConfiguration>.Filter.Empty;
            var update = Builders<AccountConfiguration>.Update.Set(x => x.DispatchChannels, dispatchChannels);
            var opts = new FindOneAndUpdateOptions<AccountConfiguration> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            return await _AccountConfiguration.FindOneAndUpdateAsync<AccountConfiguration>(filter, update, opts);
        }

        public async Task<AccountConfiguration> UpdateAccountConfiguration_Vendors(List<Vendor> vendors)
        {
            var filter = Builders<AccountConfiguration>.Filter.Empty;
            var update = Builders<AccountConfiguration>.Update.Set(x => x.Vendors, vendors);
            var opts = new FindOneAndUpdateOptions<AccountConfiguration> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            return await _AccountConfiguration.FindOneAndUpdateAsync<AccountConfiguration>(filter, update, opts);
        }

        public async Task<AccountConfiguration> UpdateAccountConfiguration_PrefillSlices(List<PrefillSlicing> questions)
        {
            var filter = Builders<AccountConfiguration>.Filter.Empty;
            var update = Builders<AccountConfiguration>.Update.Set(x => x.PrefillsForSlices, questions);
            var opts = new FindOneAndUpdateOptions<AccountConfiguration> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            return await _AccountConfiguration.FindOneAndUpdateAsync<AccountConfiguration>(filter, update, opts);
        }

        public async Task<AccountConfiguration> UpdateAccountConfiguration_SMTPSetting(CustomSMTPSetting customSMTPSettings)
        {
            var filter = Builders<AccountConfiguration>.Filter.Empty;
            var update = Builders<AccountConfiguration>.Update.Set(x => x.CustomSMTPSetting, customSMTPSettings);
            var opts = new FindOneAndUpdateOptions<AccountConfiguration> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            return await _AccountConfiguration.FindOneAndUpdateAsync<AccountConfiguration>(filter, update, opts);
        }

        public async Task<AccountConfiguration> UpdateAccountConfiguration_Queue(Queue queue)
        {
            var filter = Builders<AccountConfiguration>.Filter.Empty;
            var update = Builders<AccountConfiguration>.Update.Set(x => x.Queue, queue);
            var opts = new FindOneAndUpdateOptions<AccountConfiguration> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            return await _AccountConfiguration.FindOneAndUpdateAsync<AccountConfiguration>(filter, update, opts);
        }

        public async Task<AccountConfiguration> UpdateAccountConfiguration_ExtendedProperties(Dictionary<string, string> extendedProperties)
        {
            var filter = Builders<AccountConfiguration>.Filter.Empty;
            var update = Builders<AccountConfiguration>.Update.Set(x => x.ExtendedProperties, extendedProperties);
            var opts = new FindOneAndUpdateOptions<AccountConfiguration> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            return await _AccountConfiguration.FindOneAndUpdateAsync<AccountConfiguration>(filter, update, opts);
        }

        public async Task DeleteAccountConfiguration()
        {
            var filter = Builders<AccountConfiguration>.Filter.Empty;
            await _AccountConfiguration.FindOneAndDeleteAsync<AccountConfiguration>(filter);
            return;
        }

        public async Task AddUnsubscribeRecord(string email)
        {
            try
            {
                long recordcount = await _Unsubscribe.CountDocumentsAsync(x => x.Email == email);
                if (recordcount == 0)
                {
                    Unsubscribed newRecord = new Unsubscribed()
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Email = email?.ToLower(),
                        UnsubscribedAt = DateTime.UtcNow
                    };
                    await _Unsubscribe.InsertOneAsync(newRecord);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task RemoveUnsubscribeRecord(string id)
        {
            await _Unsubscribe.DeleteOneAsync(x => x.Email.Equals(id));
        }

        public async Task<bool> CheckUnsubscribe(string email)
        {
            try
            {
                long count = await _Unsubscribe.CountDocumentsAsync(x => x.Email.Equals(email));
                if (count > 0)
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #region LogEvent
        public async Task AddLogEvent(LogEvent logevents)
        {

            if (logevents == null)
                return;
            logevents.Id= ObjectId.GenerateNewId().ToString();
            logevents.Created = DateTime.UtcNow;

            await _EventLog.InsertOneAsync(logevents);
        }

        public async Task AddLogEvents(List<LogEvent> logevents)
        {
            if (logevents == null || logevents.Count == 0)
                return;
            foreach(var logevent in logevents.ToList())
            {
                if (CheckLogLevel(logevent.LogMessage.Level) > _maximumLevel)
                    logevents.Remove(logevent);
            }
            if (logevents.Count == 0)
                return;
            await _EventLog.InsertManyAsync(logevents);
        }

        public int CheckLogLevel(string message)
        {
            if (LogMessage.SeverityLevel_Critical == message)
                return 1;
            else if (LogMessage.SeverityLevel_Critical == message)
                return 2;
            else if (LogMessage.SeverityLevel_Critical == message)
                return 3;
            else if (LogMessage.SeverityLevel_Critical == message)
                return 4;
            else
                return 5;
        }

        public async Task UpdateBulkEventLog(Dictionary<LogEvent, InvitationLogEvent> logevents)
        {
            var builder = Builders<LogEvent>.Filter;
            var bulkEventLogList = new List<WriteModel<LogEvent>>();
            try
            {
                foreach (var logEvent in logevents)
                {
                    var invitationEvent = logEvent.Value;
                    //invitationEvent.TimeStamp = DateTime.UtcNow;

                    var updateUserData = Builders<LogEvent>.Update.Push(x => x.Events, invitationEvent)
                        .Set(x => x.TokenId, logEvent.Key.TokenId.ToUpper())
                        .Set(x => x.Updated, logEvent.Key.Updated);

                    bulkEventLogList.Add(new UpdateManyModel<LogEvent>(builder.Eq(x => x.BatchId, logEvent.Key.BatchId) 
                        & builder.Eq(x => x.TargetHashed, logEvent.Key.TargetHashed) 
                        & builder.Eq(x => x.DispatchId, logEvent.Key.DispatchId)
                        & builder.ElemMatch(x => x.Events, x => x.Action == 0), updateUserData));
                }

                var result = await _EventLog.BulkWriteAsync(bulkEventLogList, new BulkWriteOptions() { IsOrdered = false });
                if (result.IsAcknowledged)
                {
                    //add debug log for success
                }
                else
                {
                    //add debug log for failure 
                }

            }
            catch (Exception ex0)
            {
                await AddExceptionEvent(ex0);
            }

        }

        public async Task AddOrUpdateEvent(LogEvent logevents, InvitationLogEvent logevent = null, string tokenId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(logevents.Id) )
                    logevents.Id = ObjectId.GenerateNewId().ToString();

                logevents.TokenId = tokenId;
                var utcNow = DateTime.UtcNow;
                var filter = Builders<LogEvent>.Filter.Eq(x => x.Id, logevents.Id);
                var update = Builders<LogEvent>.Update;
                List<UpdateDefinition<LogEvent>> updates = new List<UpdateDefinition<LogEvent>>();

                updates.Add(update.SetOnInsert(x => x.Created, utcNow));
                updates.Add(update.SetOnInsert(x => x.Location, logevents.Location));
                updates.Add(update.SetOnInsert(x => x.DeliveryWorkFlowId, logevents.DeliveryWorkFlowId));
                updates.Add(update.SetOnInsert(x => x.Target, logevents.Target));
                updates.Add(update.SetOnInsert(x => x.TargetHashed, logevents.TargetHashed));
                updates.Add(update.SetOnInsert(x => x.LogMessage, logevents.LogMessage));
                updates.Add(update.SetOnInsert(x => x.Events, logevents.Events));

                if (logevent!=null)
                {
                    logevent.TimeStamp = utcNow;
                    updates.Add(update.Push(x => x.Events, logevent));
                }

                updates.Add(update.Set(x => x.Updated, utcNow));

                var opt = new FindOneAndUpdateOptions<LogEvent> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
                var up = update.Combine(updates);
                await _EventLog.FindOneAndUpdateAsync(filter, up, opt);

            }
            catch (Exception ex)
            {
                await AddExceptionEvent(ex);
            }
        }
        public async Task  AddBulkEvents(List<LogEvent> documents)
        {

            if (documents == null)
                return;
            try
            {
                await _EventLog.InsertManyAsync(documents, new InsertManyOptions() { IsOrdered = false });
            }
            catch (Exception ex0)
            {
                await AddExceptionEvent(ex0);
            }
           
        }
        public async Task AddExceptionEvent(Exception ex0, string batchId = null, string dispatchId = null, string deliveryPlanId = null, string questionnaire= null,string message = null)
        {
            List<string> tags = new List<string>();
            if (string.IsNullOrWhiteSpace(dispatchId))
                tags.Add("Account");
             
            var logEvent = new LogEvent()
            {
                BatchId = batchId,
                DispatchId = dispatchId,
                DeliveryWorkFlowId = deliveryPlanId,
                Location = questionnaire,
                LogMessage = new LogMessage() { Exception = JsonConvert.SerializeObject(ex0) ,Level = LogMessage.SeverityLevel_Critical, Message = message  },
                Tags = tags
                
            };
            await AddLogEvent(logEvent);
        }
        public async Task AddEventByLevel(int level, string message, string batchId, string dispatchId = null, string deliveryPlanId = null, string questionnaire = null)
        {
            if (level > _maximumLevel)
                return;
            string CriticalityLevel;
            if (level == 1)
                CriticalityLevel = LogMessage.SeverityLevel_Critical;
            else if (level == 2)
                CriticalityLevel = LogMessage.SeverityLevel_Error;
            else if (level == 3)
                CriticalityLevel = LogMessage.SeverityLevel_Information;
            else if (level == 4)
                CriticalityLevel = LogMessage.SeverityLevel_Warning;
            else
                CriticalityLevel = LogMessage.SeverityLevel_Verbose;
            
            var logEvent = new LogEvent()
            {
                BatchId = batchId,
                DispatchId = dispatchId,
                DeliveryWorkFlowId = deliveryPlanId,
                Location = questionnaire,
                LogMessage =  new LogMessage() { Message =message,  Level = CriticalityLevel }
            };
            await AddLogEvent(logEvent);

        }


        #endregion

        #region dp reporting

        public async Task<List<LogEvent>> GetDeliveryEvents(FilterBy filter)
        {
            if (filter == null)
                return null;

            return await _EventLog.Find(x => x.Events != null &&
            x.Updated > filter.afterdate && x.Updated < filter.beforedate).ToListAsync();
        }

        //method to merge WXM data and dispatch data
        public async Task<List<WXMPartnerMerged>> GetMergedData(FilterBy filter, string bearer, List<Question> questions, int skip, int limit, int SortOrder)
        {
            if (filter == null)
                return null;

            List<LogEvent> events = null;

            if (SortOrder == 1)
            {
                events = await _EventLog.Find(x => (x.Tags.Contains("UserData") &&
                                                        x.Updated > filter.afterdate &&
                                                        x.Updated < filter.beforedate) ||
                                                        ((x.Tags.Contains("DispatchAPI") ||
                                                        x.Tags.Contains("Initiator")) &&
                                                        x.Created > filter.afterdate &&
                                                        x.Created < filter.beforedate &&
                                                        (x.LogMessage.Level == "Error" ||
                                                        x.LogMessage.Level == "Failure"))).SortBy(x => x.Created).Skip(skip).Limit(limit).ToListAsync();
            }
            else
            {
                events = await _EventLog.Find(x => (x.Tags.Contains("UserData") &&
                                                        x.Updated > filter.afterdate &&
                                                        x.Updated < filter.beforedate) ||
                                                        ((x.Tags.Contains("DispatchAPI") ||
                                                        x.Tags.Contains("Initiator")) &&
                                                        x.Created > filter.afterdate &&
                                                        x.Created < filter.beforedate &&
                                                        (x.LogMessage.Level == "Error" ||
                                                        x.LogMessage.Level == "Failure"))).SortByDescending(x => x.Created).Skip(skip).Limit(limit).ToListAsync();
            }

            if (events == null)
                return null;

            List<string> tokens = events?.Where(x => x.TokenId != null).Select(x => x.TokenId)?.ToList();

            double.TryParse(Configuration["DataUploadSettings:CheckResponsesCapturedForLastHours"], out double LastHours);

            //wxm filter to filter out responses on wxm for this time range.. dp events are taken using the token ids present
            FilterBy WXMFilter = new FilterBy { afterdate = DateTime.UtcNow.AddHours(-LastHours), beforedate = DateTime.UtcNow };

            //Already merged tokens also need to be updated with answers if answers come in late

            List<WXMPartnerMerged> unanswered = await GetUnasweredMergedData(WXMFilter, tokens);

            IEnumerable<string> Unansweredtoks = unanswered.Where(x => x._id?.ToLower()?.Contains("NoToken") == false).Select(x => x._id);

            if (unanswered != null)
                tokens.AddRange(Unansweredtoks);

            tokens = tokens.Distinct()?.ToList();
            
            List<WXMDeliveryEvents> WXMData = await hTTPWrapper.GetWXMOperationMetrics(new WXMMergedEventsFilter { TokenIds = tokens, filter = WXMFilter }, bearer);

            if (WXMData == null)
                return null;

            //expand events variable to include unanswered older events as well to populate responses if answered
            var query = Builders<LogEvent>.Filter.In(x => x.TokenId, Unansweredtoks) &
                        Builders<LogEvent>.Filter.Ne(x => x.Events, null);

            List<LogEvent> UnansweredEvents = await _EventLog.Find(query).ToListAsync();

            foreach (var eve in UnansweredEvents)
            {
                if (events?.FindAll(x => x.TokenId == eve.TokenId)?.Count() == 0)
                    events.Add(eve);
            }

            List<WXMPartnerMerged> data = new List<WXMPartnerMerged>();

            foreach (LogEvent e in events)
            {

                try
                {
                    WXMPartnerMerged o = new WXMPartnerMerged();

                    WXMDeliveryEvents w = WXMData.Where(x => x._id == e.TokenId)?.FirstOrDefault();

                    if (w != null)
                    {
                        o._id = w._id;

                        if (w.Responses?.Count() > 0)
                        {
                            o.Responses = w.Responses;
                            o._id = w._id; //token id
                            o.Partial = w.Partial;
                            o.AnsweredAt = w.AnsweredAt;
                            o.Answered = true;
                        }
                        else
                        {
                            o._id = w._id;
                            o.Answered = false;
                        }

                        foreach (DeliveryEvent wxmops in w.WXMEvents)
                        {
                            if (wxmops.Action?.ToLower() == "unsubscribe")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(wxmops);

                                o.Unsubscribe = true;
                                o.UnsubscribeChannel = wxmops.Channel;
                                o.UnsubscribeMessage = wxmops.Message;
                            }
                            if (wxmops.Action?.ToLower() == "unsubscribed")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(wxmops);

                                o.Unsubscribed = true;
                                o.UnsubscribedChannel = wxmops.Channel;
                                o.UnsubscribedMessage = wxmops.Message;
                            }
                            if (wxmops.Action?.ToLower() == "bounced")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(wxmops);

                                o.Bounced = true;
                                o.BouncedChannel = wxmops.Channel;
                                o.BouncedMessage = wxmops.Message;
                            }
                            if (wxmops.Action?.ToLower() == "exception")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(wxmops);

                                o.Exception = true;
                                o.ExceptionCount = o.ExceptionCount + 1;
                                o.ExceptionChannel = wxmops.Channel;
                                o.ExceptionMessage = wxmops.Message;
                            }
                            if (wxmops.Action?.ToLower() == "displayed")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(wxmops);

                                o.Displayed = true;
                                o.DisplayedMessage = wxmops.Message;
                                o.DisplayedChannel = wxmops.Channel;
                            }
                            if (wxmops.Action?.ToLower() == "sent")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(wxmops);

                                o.Sent = true;

                                o.SentSequence = "Message " + (wxmops.SentSequence == null ? 0 : wxmops.SentSequence + 1)?.ToString();

                            }
                            if (wxmops.Action?.ToLower() == "throttled")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(wxmops);

                                o.Throttled = true;
                                o.ThrottledChannel = wxmops.Channel;
                                o.ThrottledMessage = wxmops.Message;
                            }
                            if (wxmops.Action?.ToLower() == "answered")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(wxmops);
                            }
                        }
                    }
                    else
                        o._id = e.TokenId; //token id

                    if (e.Prefills != null)
                    {
                        if (o.Responses != null)
                        {
                            foreach (Prefill p in e.Prefills)
                            {
                                if (o.Responses.Where(x => x.QuestionId == p.QuestionId)?.Count() == 0)
                                {
                                    o.Responses.Add(new Response
                                    {
                                        QuestionId = p.QuestionId,
                                        QuestionText = questions.Where(x => x.Id == p.QuestionId)?.FirstOrDefault()?.Text,
                                        TextInput = p.Input_Hash
                                    });
                                }
                            }
                        }
                        else
                        {
                            o.Responses = new List<Response>();

                            foreach (Prefill p in e.Prefills)
                            {
                                o.Responses.Add(new Response
                                {
                                    QuestionId = p.QuestionId,
                                    QuestionText = questions.Where(x => x.Id == p.QuestionId)?.FirstOrDefault()?.Text,
                                    TextInput = p.Input_Hash
                                });
                            }
                        }
                    }

                    //use already present object id to differentiate in this case otherwise you'll keep creating new documents in mergeddata collection
                    if (o._id == null)
                        o._id = "NoToken" + e.Id;

                    o.BatchId = e.BatchId;
                    o.DeliveryWorkFlowId = e.DeliveryWorkFlowId;
                    o.LastUpdated = e.Updated;
                    o.CreatedAt = e.Created;
                    o.DispatchId = e.DispatchId;
                    o.TargetHashed = e.TargetHashed;
                    o.Questionnaire = e.Location;

                    if (e.LogMessage != null)
                    {
                        if (o.Events == null)
                            o.Events = new List<DeliveryEvent>();

                        o.CreatedAt = e.Created;

                        DeliveryEvent LogWxmEvent = new DeliveryEvent
                        {
                            Action = e.LogMessage.Level,
                            Message = e.LogMessage.Message,
                        };

                        o.Error = true;
                        o.ErrorMessage = e.LogMessage.Message;
                        o.Requested = true;
                        o.Events.Add(LogWxmEvent);
                    }

                    if (e.Events != null)
                    {
                        foreach (InvitationLogEvent log in e.Events)
                        {
                            if (log.Action.ToString() == "Requested")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(new DeliveryEvent
                                {
                                    TimeStamp = log.TimeStamp,
                                    Channel = log.Channel.ToString(),
                                    Action = log.Action.ToString(),
                                    Message = log.Message
                                });

                                o.Requested = true;
                                o.RequestedChannel = log.Channel.ToString();
                                o.RequestedMessage = log.Message;
                            }
                            if (log.Action.ToString() == "Rejected")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(new DeliveryEvent
                                {
                                    TimeStamp = log.TimeStamp,
                                    Channel = log.Channel.ToString(),
                                    Action = log.Action.ToString(),
                                    Message = log.LogMessage.Message
                                });

                                o.Rejected = true;
                                o.RejectedChannel = log.Channel.ToString();
                                o.RejectedMessage = log.LogMessage.Message;
                            }
                            if (log.Action.ToString() == "TokenCreated")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(new DeliveryEvent
                                {
                                    TimeStamp = log.TimeStamp,
                                    Channel = log.Channel.ToString(),
                                    Action = log.Action.ToString(),
                                    Message = log.Message
                                });

                                o.TokenCreated = true;
                                o.TokenCreatedChannel = log.Channel.ToString();
                                o.TokenCreatedMessage = log.Message;
                            }
                            if (log.Action.ToString() == "Error")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(new DeliveryEvent
                                {
                                    TimeStamp = log.TimeStamp,
                                    Channel = log.Channel.ToString(),
                                    Action = log.Action.ToString(),
                                    Message = log.Message
                                });

                                o.Error = true;
                                o.ErrorChannel = log.Channel.ToString();
                                o.ErrorMessage = log.Message;
                            }
                            if (log.Action.ToString() == "Supressed")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(new DeliveryEvent
                                {
                                    TimeStamp = log.TimeStamp,
                                    Channel = log.Channel.ToString(),
                                    Action = log.Action.ToString(),
                                    Message = log.Message
                                });

                                o.Supressed = true;
                                o.SupressedChannel = log.Channel.ToString();
                                o.SupressedMessage = log.Message;
                            }
                            if (log.Action.ToString() == "DispatchSuccessful")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(new DeliveryEvent
                                {
                                    TimeStamp = log.TimeStamp,
                                    Channel = log.Channel.ToString(),
                                    Action = log.Action.ToString(),
                                    LogMessage = log.LogMessage?.Message,
                                    Message = log.Message
                                });
                            }
                            if (log.Action.ToString() == "DispatchUnsuccessful")
                            {
                                if (o.Events == null)
                                    o.Events = new List<DeliveryEvent>();

                                o.Events.Add(new DeliveryEvent
                                {
                                    TimeStamp = log.TimeStamp,
                                    Channel = log.Channel.ToString(),
                                    Action = log.Action.ToString(),
                                    LogMessage = log.LogMessage?.Message,
                                    Message = log.Message
                                });
                            }
                        }

                        //order by to avoid confusion
                        o.Events = o.Events.OrderBy(x => x.TimeStamp).ToList();
                    }

                    data.Add(o);
                }
                catch (Exception ex)
                {

                    continue;
                }
            }

            return data;
        }

        public async Task Upload(List<WXMPartnerMerged> data)
        {
            if (data == null || data?.Count() == 0)
                return;

            try
            {
                List<List<WXMPartnerMerged>> DataSplits = new List<List<WXMPartnerMerged>>();

                for (int i = 0; i < data?.Count(); i = i + 1000)
                {
                    try
                    {
                        if (i + 1000 >= data?.Count())
                        {
                            DataSplits.Add(data.GetRange(i, data.Count() - i));
                            continue;
                        }

                        DataSplits.Add(data.GetRange(i, 1000));
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                }

                foreach (var split in DataSplits)
                {
                    var bulkOps = new List<WriteModel<WXMPartnerMerged>>();

                    foreach (var dat in split)
                    {
                        var upsertOne = new ReplaceOneModel<WXMPartnerMerged>(
                            Builders<WXMPartnerMerged>.Filter.Where(x => x._id == dat._id),
                            dat)
                        { IsUpsert = true };
                        bulkOps.Add(upsertOne);
                    }
                    await _mergedData.BulkWriteAsync(bulkOps);
                    await Task.Delay(1000);
                }
            }
            catch (Exception e)
            {

            }
        }

        public async Task<OnDemandReportModel> GetOnDemandModel()
        {
            try
            {
                var filterbuilder = Builders<OnDemandReportModel>.Filter;

                return await _OnDemand.Find(_ => true).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<bool> LockOnDemand(FilterBy filter, OnDemandReportModel ExistingSetting)
        {
            try
            {
                var filterbuilder = Builders<OnDemandReportModel>.Update;

                if (ExistingSetting.Id != null)
                {
                    var update = filterbuilder.Set(x => x.Filter, filter).Set(x => x.TimeOffSet, ExistingSetting.TimeOffSet).Set(x => x.IsLocked, true).Set(x => x.OnlyLogs, ExistingSetting.OnlyLogs);
                                    
                    var result = await _OnDemand.UpdateOneAsync(x => x.Id == ExistingSetting.Id, update);

                    return result.IsAcknowledged;
                }
                else
                {
                    ExistingSetting = new OnDemandReportModel { Filter = filter, IsLocked = true, Id = ObjectId.GenerateNewId().ToString(), TimeOffSet = ExistingSetting.TimeOffSet, OnlyLogs = ExistingSetting.OnlyLogs };

                    await _OnDemand.InsertOneAsync(ExistingSetting);

                    return true;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> UnLockOnDemand()
        {
            try
            {
                var filterbuilder = Builders<OnDemandReportModel>.Filter;

                //fetch on demand current settings
                var condition = filterbuilder.Eq(x => x.IsLocked, true);

                var ExistingSetting = await _OnDemand.Find(condition).FirstOrDefaultAsync();

                if (ExistingSetting != null)
                {
                    var filter = Builders<OnDemandReportModel>.Filter.Eq(x => x.Id, ExistingSetting.Id);
                    var update = Builders<OnDemandReportModel>.Update.Set(x => x.IsLocked, false);
                    var result = _OnDemand.UpdateOneAsync(filter, update).Result;

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<WXMPartnerMerged>> GetUnasweredMergedData(FilterBy filter,List<string> tokens)
        {
            try
            {
                var filterbuilder = Builders<WXMPartnerMerged>.Filter;

                var condition = filterbuilder.Eq(x => x.Answered, false) &
                                filterbuilder.Gte(x => x.CreatedAt, filter.afterdate) &
                                filterbuilder.Lt(x => x.CreatedAt, filter.beforedate) &
                                filterbuilder.Ne(x => x._id, null) & 
                                filterbuilder.In(x => x._id, tokens);

                var fields = Builders<WXMPartnerMerged>.Projection.Include(x => x._id);

                return await _mergedData.Find(condition).Project<WXMPartnerMerged>(fields).ToListAsync();
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<long> GetMergedDataCount(FilterBy filter)
        {
            if (filter == null)
                return 0;

            try
            {
                return await _mergedData.CountDocumentsAsync(x => x.CreatedAt > filter.afterdate &&
                                                                 x.CreatedAt < filter.beforedate);
            }
            catch (Exception e)
            {
                return 0;
            }
        }

        public async Task<List<WXMPartnerMerged>> GetMergedDataFromDb(FilterBy filter, int skip, int limit)
        {
            if (filter == null)
                return null;

            try
            {
                var filterBuilder = Builders<WXMPartnerMerged>.Filter;
                var mongofilter = filterBuilder.Gte(x => x.CreatedAt, filter.afterdate) &
         filterBuilder.Lt(x => x.CreatedAt, filter.beforedate);

                var a = await _mergedData.Find(mongofilter)?.Skip(skip).Limit(limit).ToListAsync();
                
                return a;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<List<string>> GetQuestionnairesUsed()
        {
            var builder = Builders<LogEvent>.Filter;
            var filter = builder.Ne(x => x.Events, null);
            List<string> distinct = await _EventLog.Distinct<string>("Location", filter).ToListAsync<string>();

            return distinct;
        }

        public async Task<List<RequestInitiatorRecords>> GetRequestInitiatorRecords()
        {
            try
            {
                return (await _RequestInitiator.FindAsync(x => x.Id != null)).ToList();
            }
            catch(Exception e)
            {
                return null;
            }
        }

        public async Task<long> GetEventsCount(FilterBy filter)
        {
            if (filter == null)
                return 0;

            try
            {
                return await _EventLog.CountDocumentsAsync(x => (x.Tags.Contains("UserData") &&
                                                        x.Updated > filter.afterdate &&
                                                        x.Updated < filter.beforedate) ||
                                                        ((x.Tags.Contains("DispatchAPI") ||
                                                        x.Tags.Contains("Initiator")) &&
                                                        x.Created > filter.afterdate &&
                                                        x.Created < filter.beforedate &&
                                                        (x.LogMessage.Level == "Error" ||
                                                        x.LogMessage.Level == "Failure")));
                //return await _EventLog.CountDocumentsAsync(x => x.Created > filter.afterdate &&
                //                                                 x.Created < filter.beforedate);
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }
}
