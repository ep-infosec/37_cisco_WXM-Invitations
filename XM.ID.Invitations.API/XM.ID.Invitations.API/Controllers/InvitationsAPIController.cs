using XM.ID.Invitations.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XM.ID.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Invitations.Controllers
{
    [ApiController]
    [Route("api")]
    public class InvitationsAPIController : ControllerBase
    {
        private readonly IConfiguration Config;
        private readonly AuthTokenValidation AuthTokenValidation;
        private readonly ViaMongoDB ViaMongoDB;
        private readonly PayloadValidation PayloadValidation;
        private readonly EventLogList EventLogList;

        public InvitationsAPIController(IConfiguration config, AuthTokenValidation authTokenValidation,
            ViaMongoDB viaMongoDB, PayloadValidation payloadValidation, EventLogList eventLogList)
        {
            Config = config;
            AuthTokenValidation = authTokenValidation;
            ViaMongoDB = viaMongoDB;
            PayloadValidation = payloadValidation;
            EventLogList = eventLogList;
        }

        [HttpPost]
        [Route("dispatchRequest")]
        public async Task<IActionResult> DispatchRequest([FromHeader(Name = "Authorization")] string authToken,
            List<DispatchRequest> request, [FromHeader(Name = "BatchID")] string batchId = null)
        {
            try
            {
                if (request == null)
                    return BadRequest("Bad Request");

                // Fetch account configuration to be used through the whole request.
                AccountConfiguration accConfiguration = GetAccountConfiguration().Result;
                if (accConfiguration == null)
                {
                    EventLogList.AddEventByLevel(2, SharedSettings.NoConfigInSPA, null);
                    await EventLogList.AddEventLogs(ViaMongoDB);
                    return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError, SharedSettings.NoConfigInSPA);
                }

                // Validate Auth token(Basic or Bearer) and reject if fail.
                if (!AuthTokenValidation.ValidateBearerToken(authToken, accConfiguration))
                {
                    EventLogList.AddEventByLevel(2, SharedSettings.AuthorizationDenied, null, null);
                    await EventLogList.AddEventLogs(ViaMongoDB);
                    return Unauthorized(SharedSettings.AuthDeniedResponse);
                }

                // Check for Payload size and number of Dispatches
                if (!PayloadValidation.ValidateRequestPayloadSize(request, EventLogList))
                {
                    await EventLogList.AddEventLogs(ViaMongoDB);
                    return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status413PayloadTooLarge, SharedSettings.PayLoadTooLarge);
                }

                //Generate batch ID for the request
                if (string.IsNullOrEmpty(batchId))
                    batchId = Guid.NewGuid().ToString();

                // Check for sampling
                if (accConfiguration.ExtendedProperties.TryGetValue("Sampler", out string samplername))
                    if (SharedSettings.AvailableSamplers.TryGetValue(samplername, out ISampler sampler))
                        await sampler.IsSampledAsync(request);
                    else
                    {
                        EventLogList.AddEventByLevel(4, SharedSettings.NoSamplingConfigured, batchId);
                    }

                BatchResponse batchResponse = new BatchResponse()
                {
                    BatchId = batchId,
                    StatusByDispatch = new List<StatusByDispatch>()
                };

                try
                {
                    ProcessInvitations processInvitations = new ProcessInvitations(authToken, ViaMongoDB, batchId,
                        EventLogList, accConfiguration);

                    bool res = processInvitations.GetAllInfoForDispatch();
                    if (!res)
                    {
                        EventLogList.AddEventByLevel(2, SharedSettings.APIResponseFail, batchId, null);
                        await EventLogList.AddEventLogs(ViaMongoDB);
                        return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError,
                            SharedSettings.APIResponseFail);
                    }

                    await processInvitations.CheckDispatchData(request, batchId, batchResponse);
                }
                catch (Exception ex)
                {
                    EventLogList.AddExceptionEvent(ex, batchId, null, null, null, SharedSettings.DispatchControllerEx2);
                    await EventLogList.AddEventLogs(ViaMongoDB);
                    return ex.Message switch
                    {
                        SharedSettings.AuthorizationDenied => Unauthorized(ex.Message),
                        _ => StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError, ex.Message)
                    };
                }

                EventLogList.AddEventByLevel(5, SharedSettings.DispatchStatusReturned, batchId, null);
                await EventLogList.AddEventLogs(ViaMongoDB);

                return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status207MultiStatus, batchResponse);

            }
            catch (Exception ex)
            {
                EventLogList.AddExceptionEvent(ex, null, null, null, null, SharedSettings.DispatchControllerEx1);
                await EventLogList.AddEventLogs(ViaMongoDB);
                return ex.Message switch
                {
                    SharedSettings.AuthorizationDenied => Unauthorized(ex.Message),
                    _ => StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError, ex.Message)
                };
            }
        }

        private async Task<AccountConfiguration> GetAccountConfiguration()
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

            return accountConfiguration;
        }

        [HttpPost]
        [Route("EventLog")]
        public async Task<IActionResult> GetEventLog([FromHeader(Name = "Authorization")] string authToken,
            ActivityFilter filterObject)
        {
            //{"BatchId":"","DispatchId":"","Token":"","Created":"","Target":""} request format
            CultureInfo provider = CultureInfo.InvariantCulture;
            try
            {
                // Validate Auth token(Basic or Bearer) and reject if fail.
                if (!await AuthTokenValidation.ValidateBearerToken(authToken))
                {
                    return Unauthorized(SharedSettings.AuthorizationDenied);
                }

                if (string.IsNullOrWhiteSpace(filterObject.BatchId) &&
                    string.IsNullOrWhiteSpace(filterObject.DispatchId) &&
                    string.IsNullOrWhiteSpace(filterObject.Token) &&
                    string.IsNullOrWhiteSpace(filterObject.UUID) &&
                    string.IsNullOrWhiteSpace(filterObject.Created))
                    return BadRequest("EventLog filters are empty.");

                if (!string.IsNullOrWhiteSpace(filterObject.FromDate) 
                    || !string.IsNullOrWhiteSpace(filterObject.ToDate))
                {
                    if (!DateTime.TryParseExact(filterObject.FromDate, "dd/MM/yyyy", provider, DateTimeStyles.None,
                out DateTime fromdate) || !DateTime.TryParseExact(filterObject.ToDate,
                 "dd/MM/yyyy", provider, DateTimeStyles.None, out DateTime todate))
                        throw new Exception("Date format was not correct. Use (dd/MM/yyyy) format for date.");
                }
                
                var response = await ViaMongoDB.GetActivityDocuments(filterObject);
                return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine("exception: ", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("dispatchSingle/{dispatchID}")]

        public async Task<IActionResult> DispatchSingle(string dispatchID, [FromHeader(Name = "Authorization")] string authToken)
        {
            try
            {
                var parameters = Request.Query.ToDictionary(q => q.Key, q => q.Value);

                if (parameters.Count == 0)
                    return BadRequest();

                List<DispatchRequest> dispatchRequests = new List<DispatchRequest>();
                DispatchRequest dispatchRequest = new DispatchRequest()
                {
                    DispatchID = dispatchID,
                    PreFill = new List<List<PreFillValue>>()
                };
                List<PreFillValue> preFillValues = new List<PreFillValue>();
                foreach (var parameter in parameters)
                {
                    PreFillValue preFillValue = new PreFillValue()
                    {
                        QuestionId = parameter.Key,
                        Input = parameter.Value
                    };
                    preFillValues.Add(preFillValue);
                }
                dispatchRequest.PreFill.Add(preFillValues);
                dispatchRequests.Add(dispatchRequest);
                return await DispatchRequest(authToken, dispatchRequests);
            }
            catch (Exception ex)
            {
                Console.WriteLine("exception", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("MetricsReport/{OnlyLogs}")]
        public async Task<IActionResult> GetDpReport([FromHeader(Name = "Authorization")] string authToken,
            ACMInputFilter InputFilter, bool OnlyLogs = false)
        {
            //{"afterdate":"","beforedate":""} request format
            try
            {
                //Validate Auth token(Basic or Bearer) and reject if fail.
                if (!await AuthTokenValidation.ValidateBearerToken(authToken))
                {
                    return Unauthorized(SharedSettings.AuthorizationDenied);
                }

                FilterBy filter = new FilterBy();

                try
                {
                    //request coming in is considered to be in user time so you have to get to UTC from here
                    filter.afterdate = DateTime.ParseExact(InputFilter.afterdate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                    filter.beforedate = DateTime.ParseExact(InputFilter.beforedate, "dd/MM/yyyy", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1);
                }
                catch(Exception e)
                {
                    return BadRequest("Entered date format is not correct. Please enter the date in dd/MM/yyyy format only.");
                }

                int TimeZoneOffset = 0;

                try
                {
                    string offset = InvitationsMemoryCache.GetInstance().GetFromMemoryCache(authToken.Split(' ')[1]);

                    //conversion to UTC
                    if (!string.IsNullOrEmpty(offset))
                    {
                        TimeZoneOffset = Convert.ToInt32(offset);

                        //do opposite of offset from UTC to get to UTC
                        if (TimeZoneOffset < 0)
                        {
                            filter.afterdate = filter.afterdate.AddMinutes(Math.Abs(TimeZoneOffset));
                            filter.beforedate = filter.beforedate.AddMinutes(Math.Abs(TimeZoneOffset));
                        }
                        else
                        {
                            filter.afterdate = filter.afterdate.AddMinutes(-Math.Abs(TimeZoneOffset));
                            filter.beforedate = filter.beforedate.AddMinutes(-Math.Abs(TimeZoneOffset));
                        }
                    }
                    else
                    {
                        return BadRequest("Unable to convert the entered date range to UTC. Please Re-Login and contact administrator if issue persists");
                    }
                }
                catch (Exception e)
                {
                    return BadRequest("Unable to convert the entered date range to UTC. Please Re-Login and contact administrator if issue persists");
                }

                if (filter.afterdate.Year == 0001 || filter.beforedate.Year == 0001)
                    return BadRequest("There are no dates provided in the request. Please enter valid dates and try again.");

                if ((filter.beforedate - filter.afterdate).Days > 90)
                    return BadRequest("Entered date range is too long. Reports can be downloaded for 90 days of date range only.");

                AccountConfiguration a = await ViaMongoDB.GetAccountConfiguration();

                if (a.CustomSMTPSetting == null)
                    return BadRequest("No smtp details configured");

                //check to see if emails are configured
                string Emails = null;
                if (!a.ExtendedProperties?.Keys?.Contains("ReportRecipients") == true)
                {
                    return BadRequest("There is some configuration mismatch encountered during storing the Email IDs. Email IDs are supposed to be passed under \"ReportRecipients\" key only. Please retry again and contact your administrator if error still occurs.");
                }
                else if (string.IsNullOrEmpty(a.ExtendedProperties["ReportRecipients"]))
                {
                    return BadRequest("There are no EmailIDs provided which are mandatory before Reports Generation request.");
                }
                else
                {
                    Emails = a.ExtendedProperties["ReportRecipients"];
                }

                bool IsValidEmail(string email)
                {
                    try
                    {
                        var mail = new System.Net.Mail.MailAddress(email);
                        return true;
                    }
                    catch (Exception e)
                    {
                        return false;
                    }
                }

                foreach (string email in Emails.Split(";"))
                {
                    string e = Regex.Replace(email, @"\s+", "");
                    if (!IsValidEmail(e))
                        BadRequest("The format of one or more Email IDs configured is incorrect. Please make sure all the Email IDs are sent in valid format only.");
                }

                long count = await ViaMongoDB.GetMergedDataCount(filter);

                if (count == 0)
                    return BadRequest("No data available for selected date range. Please try another date range.");

                OnDemandReportModel OnDemand = await ViaMongoDB.GetOnDemandModel();

                if ((OnDemand != null && OnDemand.IsLocked == false /*&& OnDemand.IsMerging == false*/) || OnDemand == null)
                {
                    if (OnDemand == null)
                        OnDemand = new OnDemandReportModel();

                    OnDemand.TimeOffSet = TimeZoneOffset;
                    OnDemand.OnlyLogs = OnlyLogs;

                    var response = await ViaMongoDB.LockOnDemand(filter, OnDemand);

                    if (response == true)
                        return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Your report has been sent for processing");
                    else
                        return BadRequest("An unknown error occured while generating the report . Please make sure Email IDs and Date ranges are provided in valid format only. Please retry again and contact your administrator if error still occurs.");
                }
                else if (OnDemand != null && OnDemand.IsLocked == true)
                {
                    return BadRequest("A report is being generated right now. It will be emailed to the listed recipients once it's generated. You can request another report only after this request is completed.");
                }
                else
                {
                    return BadRequest("A report is being generated right now and some setting couldn't be retrieved at this time. Please try after some time. ");
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine("exception: ", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("GetPrefillSlices")]
        public async Task<IActionResult> GetQuestionsForAnalytics([FromHeader(Name = "Authorization")] string authToken)
        {
            try
            {
                //Validate Auth token(Basic or Bearer) and reject if fail.
                if (!await AuthTokenValidation.ValidateBearerToken(authToken))
                {
                    return Unauthorized(SharedSettings.AuthorizationDenied);
                }

                List<PrefillSlicing> prefills = (await ViaMongoDB.GetAccountConfiguration())?.PrefillsForSlices;

                if (prefills != null)
                    return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, prefills);
                else
                    return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, 
                        (await ViaMongoDB.UpdateAccountConfiguration_PrefillSlices(new List<PrefillSlicing>()))?.PrefillsForSlices);
            }
            catch (Exception ex)
            {
                Console.WriteLine("exception: ", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("GetQualifiedPrefills")]
        public async Task<IActionResult> GetQualifiedPrefills([FromHeader(Name = "Authorization")] string authToken)
        {
            try
            {
                //Validate Auth token(Basic or Bearer) and reject if fail.
                if (!await AuthTokenValidation.ValidateBearerToken(authToken))
                {
                    return Unauthorized(SharedSettings.AuthorizationDenied);
                }

                HTTPWrapper hTTPWrapper = new HTTPWrapper();

                string q = InvitationsMemoryCache.GetInstance().GetActiveQuestionsFromMemoryCache(authToken, hTTPWrapper);

                if (!string.IsNullOrEmpty(q))
                {
                    List<Question> Questions = JsonConvert.DeserializeObject<List<Question>>(q);

                    var qualified = Questions.Where(x => (x.StaffFill || x.ApiFill) && x.DisplayType?.ToLower() == "select" && x.MultiSelect?.Count() > 0);

                    return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, qualified);
                }
                else
                {
                    return BadRequest("Unable to fetch questions from cache. Logout and login. If problem persists contact admin");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("exception: ", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("SetPrefillSlices")]
        public async Task<IActionResult> SetQuestionsForAnalytics([FromHeader(Name = "Authorization")] string authToken,
            List<PrefillSlicing> Questions)
        {
            try
            {
                //Validate Auth token(Basic or Bearer) and reject if fail.
                if (!await AuthTokenValidation.ValidateBearerToken(authToken))
                {
                    return Unauthorized(SharedSettings.AuthorizationDenied);
                }

                var prefills = (await ViaMongoDB.UpdateAccountConfiguration_PrefillSlices(Questions))?.PrefillsForSlices;

                if (prefills != null)
                    return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, prefills);
                else
                    return BadRequest("Unable to set prefill slices");
            }
            catch (Exception ex)
            {
                Console.WriteLine("exception: ", ex.Message);
                return BadRequest(ex.Message);
            }
        }

    }
}