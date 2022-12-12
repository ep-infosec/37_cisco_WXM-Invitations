using Cronos;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Invitations.Net
{
    public class DispatchTask : BackgroundService
    {
        private readonly ViaMongoDB viaMongoDB;
        private System.Timers.Timer _timer;
        private readonly CronExpression _expression;
        private readonly TimeZoneInfo _timeZoneInfo;
     
        public DispatchTask(IScheduleConfig<DispatchTask> config, ViaMongoDB viaMongoDB)
        {
            this.viaMongoDB = viaMongoDB;
            _expression = CronExpression.Parse(config.CronExpression);
            _timeZoneInfo = config.TimeZoneInfo;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await ReadQueue(stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Stop();
            await Task.CompletedTask;
        }

        public override void Dispose()
        {
            _timer?.Dispose();
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await ScheduleJob(cancellationToken);
        }

        protected async Task ScheduleJob(CancellationToken cancellationToken)
        {
            var next = _expression.GetNextOccurrence(DateTimeOffset.Now, _timeZoneInfo);
            if (next.HasValue)
            {
                var delay = next.Value - DateTimeOffset.Now;
                _timer = new System.Timers.Timer(delay.TotalMilliseconds);
                _timer.Elapsed += async (sender, args) =>
                {
                    _timer.Stop();  // reset timer
                    await ExecuteAsync(cancellationToken);
                    await ScheduleJob(cancellationToken);    // reschedule next
                };
                _timer.Start();
            }
            await Task.CompletedTask;
        }

        public async Task ReadQueue(CancellationToken cancellationToken)
        {
            EventLogList eventLog = new EventLogList();
            try
            {
                DateTime stopTime = DateTime.Now.AddSeconds(30);

                Dictionary<string, RequestBulkToken> finalBulkStorage = new
                    Dictionary<string, RequestBulkToken>();

                while (DateTime.Now < stopTime)
                {
                    RequestBulkToken requestBulkToken;
                    if (!SingletonConcurrentQueue<RequestBulkToken>.Instance.TryPeek(out requestBulkToken))
                    {
                        break;
                    }
                    else if (SingletonConcurrentQueue<RequestBulkToken>.Instance.TryDequeue(out requestBulkToken))
                    {
                        if (finalBulkStorage.ContainsKey(requestBulkToken.DispatchId))
                        {
                            finalBulkStorage[requestBulkToken.DispatchId].PrefillReponse.AddRange(requestBulkToken.PrefillReponse);
                        }
                        else
                        {
                            finalBulkStorage.Add(requestBulkToken.DispatchId, new RequestBulkToken()
                            {
                                DispatchId = requestBulkToken.DispatchId,
                                UUID = requestBulkToken.UUID,
                                Batchid = requestBulkToken.Batchid,
                                PrefillReponse = requestBulkToken.PrefillReponse
                            });
                        }
                    }
                }


                if (finalBulkStorage.Count != 0)
                {
                    HTTPWrapper hTTPWrapper = new HTTPWrapper(string.Empty, eventLog);
                    string authToken = InvitationsMemoryCache.GetInstance().GetFromMemoryCache("AuthToken");
                    if (authToken == null)
                    {
                        AccountConfiguration accountConfiguration;
                        string accountConfigurationCache = InvitationsMemoryCache.GetInstance().GetFromMemoryCache("accountconfig");
                        if (accountConfigurationCache == null)
                            accountConfiguration = await viaMongoDB.GetAccountConfiguration();
                        else
                            accountConfiguration = Newtonsoft.Json.JsonConvert.DeserializeObject<AccountConfiguration>(accountConfigurationCache);
                        string username = accountConfiguration.WXMUser;
                        string apikey = accountConfiguration.WXMAPIKey;
                        string responseBody = await hTTPWrapper.GetLoginToken(username, apikey);
                        if (!string.IsNullOrEmpty(responseBody))
                        {
                            BearerToken loginToken = Newtonsoft.Json.JsonConvert.DeserializeObject<BearerToken>(responseBody);
                            authToken = "Bearer " + loginToken.AccessToken;
                            var Expirationtime = loginToken.ExpiresIn - 300;  // Expire 5 min before for uninterrupted token creation
                            InvitationsMemoryCache.GetInstance().SetBulkTokenAuthToMemoryCache("AuthToken", authToken, Expirationtime);
                        }
                        else
                        {
                            //when login token api failed.
                            eventLog.AddEventByLevel(1, SharedSettings.BearerTokenNotGenerated, null);
                            await eventLog.AddEventLogs(viaMongoDB);
                        }

                    }

                    // Calling bulk token api sequentially
                    if (!string.IsNullOrWhiteSpace(authToken))
                    {
                        List<(string, List<BulkTokenResult>)> status = new List<(string, List<BulkTokenResult>)>();

                        foreach (var request in finalBulkStorage)
                        {
                            var response = await hTTPWrapper.BulkTokenAPI(authToken, request.Value);
                            status.Add(response);
                            Thread.Sleep(1000);  // Sleep for 1 second before making another call
                        }


                        /*
                        var bulkTokenAPITasks = finalBulkStorage.Values.ToList().Select(v =>
                        {
                            return hTTPWrapper.BulkTokenAPI(authToken, v);
                        });

                        (string, List<BulkTokenResult>)[] status = await Task.WhenAll(bulkTokenAPITasks);

                        */

                       
                        Dictionary<LogEvent, InvitationLogEvent> events = new Dictionary<LogEvent, InvitationLogEvent>();
                        DateTime utcNow = DateTime.UtcNow;

                        //Update tokens in DB
                        foreach (var item in status)
                        {
                            if (item.Item2 != null)
                            {
                                foreach (var perinvite in item.Item2)
                                {
                                    var logEvent = new LogEvent()
                                    {
                                        DispatchId = item.Item1,
                                        BatchId = perinvite.Batchid,
                                        Updated = utcNow,
                                        TokenId = perinvite.Token,
                                        TargetHashed = perinvite.UUID
                                    };

                                    var invitationEvent = new InvitationLogEvent()
                                    {
                                        Action = EventAction.TokenCreated,
                                        Channel = EventChannel.DispatchAPI,
                                        TimeStamp = utcNow,
                                        TargetId = perinvite.UUID

                                    };
                                    events.Add(logEvent, invitationEvent);

                                }
                            }
                        }

                        if(events.Count() > 0)
                            await viaMongoDB.UpdateBulkEventLog(events);

                        eventLog.AddEventByLevel(5, $"{SharedSettings.DBUpdateCompleted} {events.Count()}", null);
                        await eventLog.AddEventLogs(viaMongoDB);
                    }
                }
            }
            catch (Exception ex)
            {
                eventLog.AddExceptionEvent(ex, null, null, null, null, SharedSettings.BulkTokenException);
                await eventLog.AddEventLogs(viaMongoDB);
                return;
            }

        }
    }
}
