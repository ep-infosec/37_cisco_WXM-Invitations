using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XM.ID.Net;

namespace InvitationNotification
{
    class ViaDataBase
    {
        readonly IMongoCollection<LogEvent> _EventLog;
        readonly IMongoCollection<AccountConfiguration> _AccountConfiguration;
        ApplicationLog log;
        public ViaDataBase(IMongoDatabase asyncdb, ApplicationLog appLog)
        {

            _AccountConfiguration = asyncdb.GetCollection<AccountConfiguration>("AccountConfiguration");
            _EventLog = asyncdb.GetCollection<LogEvent>("EventLog");
            log = appLog;
        }

        public async Task<Dictionary<string, List<ProjectedLog>>> GetLogsToNotify(int minutes, Dictionary<string, int> dispatchByMaxLevel, int realTimeMaxLevel, bool isEDO = false, bool isAccountLevel = false)
        {

            try
            {
                if (!isAccountLevel && (dispatchByMaxLevel == null || dispatchByMaxLevel.Count < 1))
                    return null;

                var querryforMinutes = minutes + 2;
                DateTime afterTime = DateTime.UtcNow.AddMinutes(-5);

                List<string> levels = new List<string>();
                DateTime utcNow = DateTime.UtcNow;

                if (isEDO)
                    afterTime = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0);
                else
                    afterTime = DateTime.UtcNow.AddMinutes(-querryforMinutes);

                var builder = Builders<LogEvent>.Filter;
                List<FilterDefinition<LogEvent>> querycollect = new List<FilterDefinition<LogEvent>> { builder.Gte(x => x.Created, afterTime) };

                if (!isAccountLevel)
                {
                    var querryOr = new List<FilterDefinition<LogEvent>>();
                    foreach (var byDispatch in dispatchByMaxLevel)
                    {

                        var maxlevel = byDispatch.Value;
                        var minLevel = 1;
                        if (isEDO)
                        {
                            minLevel = Math.Min(realTimeMaxLevel + 1, 5);
                        }
                        else
                        {
                            maxlevel = Math.Min(realTimeMaxLevel, byDispatch.Value);
                        }
                        List<string> logLevels = new List<string>();
                        for (var i = minLevel; i <= maxlevel; i++)
                        {
                            if (i == 1)
                                logLevels.Add("Failure");
                            else if (i == 2)
                                logLevels.Add("Error");
                            else if (i == 3)
                                logLevels.Add("Information");
                            else if (i == 4)
                                logLevels.Add("Warning");
                            else if (i == 5)
                                logLevels.Add("Debug");
                        }

                        List<FilterDefinition<LogEvent>> queryByDispatch = new List<FilterDefinition<LogEvent>>();

                        if (isEDO)
                            queryByDispatch = new List<FilterDefinition<LogEvent>> { builder.Eq(x => x.DispatchId, byDispatch.Key), builder.In(x => x.LogMessage.Level, logLevels) };
                        else
                            queryByDispatch = new List<FilterDefinition<LogEvent>> { builder.Eq(x => x.DispatchId, byDispatch.Key), builder.In(x => x.LogMessage.Level, logLevels), builder.Ne(x => x.IsNotified, true) };

                        querryOr.Add(builder.And(queryByDispatch));

                    }
                    querycollect.Add(builder.Or(querryOr));
                }
                else
                {
                    List<FilterDefinition<LogEvent>> queryForAccountLevel = new List<FilterDefinition<LogEvent>>();
                    if (isEDO)
                        queryForAccountLevel = new List<FilterDefinition<LogEvent>> { builder.Eq(x => x.DispatchId, null), builder.Ne(x => x.LogMessage.Level, null) };
                    else
                        queryForAccountLevel = new List<FilterDefinition<LogEvent>> { builder.Eq(x => x.DispatchId, null), builder.Ne(x => x.LogMessage.Level, null), builder.Ne(x => x.IsNotified, true) };

                    querycollect.Add(builder.And(queryForAccountLevel));
                }


                //get count and display only first 100 
                var count = await _EventLog.CountDocumentsAsync(builder.And(querycollect));
                int limit = 1000000;
                if (count > 1000000)
                    limit = 1000000;
                var projectedLog = await _EventLog.Find(builder.And(querycollect))
                                                  .Project(x => new ProjectedLog()
                                                  {
                                                      BatchId = x.BatchId,
                                                      DispatchId = x.DispatchId,
                                                      LogLevel = x.LogMessage.Level,
                                                      Message = $"{x.TokenId} {x.LogMessage.Message} {x.LogMessage.Exception}",
                                                      Created = x.Created
                                                  })
                                                  .Limit(limit).ToListAsync();


                if (projectedLog != null && projectedLog.Count > 0)
                {
                    if (isAccountLevel)
                    {
                        return new Dictionary<string, List<ProjectedLog>>() { { "AccountLevel", projectedLog } };
                    }
                    else
                    {
                        //group by dispatch 
                        var logsByDispatch = projectedLog.GroupBy(x => x.DispatchId).ToDictionary(x => x.Key, y => y.ToList());
                        return logsByDispatch;
                    }

                }
                else
                {
                    log.logMessage += "No log events found for the query";
                    return null;
                }

            }
            catch (Exception ex0)
            {
                log.logMessage += $"{ex0.Message}    {ex0.StackTrace}\n";
            }
            return null;
        }

        public async Task<bool> MarkNotificationSent(int minutes, Dictionary<string, int> dispatchByMaxLevel, int realTimeMaxLevel)
        {

            if (dispatchByMaxLevel == null || dispatchByMaxLevel.Count < 1)
                return false;
            try
            {
                var querryforMinutes = minutes + 2;
                DateTime afterTime = DateTime.UtcNow.AddMinutes(-querryforMinutes);

                List<string> levels = new List<string>();

                var builder = Builders<LogEvent>.Filter;
                List<FilterDefinition<LogEvent>> querycollect = new List<FilterDefinition<LogEvent>> { builder.Gte(x => x.Created, afterTime) };

                var querryOr = new List<FilterDefinition<LogEvent>>();

                foreach (var byDispatch in dispatchByMaxLevel)
                {

                    var maxlevel = byDispatch.Value;
                    var minLevel = 1;

                    maxlevel = Math.Min(realTimeMaxLevel, byDispatch.Value);

                    List<string> logLevels = new List<string>();
                    for (var i = minLevel; i <= maxlevel; i++)
                    {
                        if (i == 1)
                            logLevels.Add("Failure");
                        else if (i == 2)
                            logLevels.Add("Error");
                        else if (i == 3)
                            logLevels.Add("Information");
                        else if (i == 4)
                            logLevels.Add("Warning");
                        else if (i == 5)
                            logLevels.Add("Debug");
                    }

                    List<FilterDefinition<LogEvent>> querryByDispatch = new List<FilterDefinition<LogEvent>>();

                    querryByDispatch = new List<FilterDefinition<LogEvent>> { builder.Eq(x => x.DispatchId, byDispatch.Key), builder.In(x => x.LogMessage.Level, logLevels), builder.Ne(x => x.IsNotified, true) };

                    querryOr.Add(builder.And(querryByDispatch));

                }
                querycollect.Add(builder.Or(querryOr));

                //update with notification sent
                var updateIsNotified = Builders<LogEvent>.Update
                                       .Set(x => x.IsNotified, true);
                var update = await _EventLog.UpdateManyAsync(builder.And(querycollect), updateIsNotified);
                if (update.IsAcknowledged)
                {
                    log.logMessage += $"{update.ModifiedCount} records updated as notification sent \n";
                    return true;
                }
            }
            catch (Exception ex0)
            {
                log.logMessage += $"{ex0.Message}    {ex0.StackTrace}\n";
                return false;
            }
            return false;

        }

        public async Task<AccountConfiguration> GetAccountdetails()
        {
            try
            {
                return await _AccountConfiguration.Find(x => !string.IsNullOrEmpty(x.WXMAdminUser)).FirstOrDefaultAsync();
            }
            catch (Exception ex0)
            {
                log.logMessage += $"{ex0.Message}    {ex0.StackTrace}";
                return null;
            }
        }
    }
}
