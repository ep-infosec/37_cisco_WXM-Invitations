using Amazon.S3;
using Amazon.S3.Model;
using ClosedXML.Excel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Initiator.Net
{
    internal class RequestPayload
    {
        internal S3EventLog S3EventLog { get; set; }
        internal DispatchIdAndDispatchReqApi DispatchIdAndDispatchReqApi { get; set; }
        internal DataTable DataTable { get; set; }
        private BearerToken BearerToken { get; set; }
        internal Dispatch Dispatch { get; set; }
        internal List<Question> Questions { get; set; }
        internal List<LogEvent> LogEvents { get; set; } = new List<LogEvent>();
        internal WXMService WXMService { get; set; } = Resources.GetInstance().WXMService;
        internal string BatchId;

        internal bool IsTargetFileUploadDirectoryValid { get; set; }
        internal bool IsFileSplitted { get; set; }
        internal bool IsConfigFileAvailableAndNotEmpty { get; set; }
        internal bool IsTargetFileAvailableAndNotEmpty { get; set; }
        internal bool IsConfigFileValid { get; set; }
        internal bool IsLoginPossible { get; set; }
        internal bool IsDispatchValid { get; set; }
        internal bool IsTargetFileHeaderWiseValid { get; set; }
        internal bool IsTargetFileRowWiseValid { get; set; }

        public RequestPayload(S3EventLog s3EventLog)
        {
            S3EventLog = s3EventLog;
            S3EventLog.BucketName = System.Web.HttpUtility.UrlDecode(s3EventLog.BucketName);
            S3EventLog.KeyName = System.Web.HttpUtility.UrlDecode(s3EventLog.KeyName);
            LogEvents.Add(Utils.CreateLogEvent(this, IRILM.S3EventReceived));
            if (!S3EventLog.KeyName.Contains("/"))
            {
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.InvalidTargetFileUploadDirectory));
                IsTargetFileUploadDirectoryValid = false;
            }
            else
                IsTargetFileUploadDirectoryValid = true;
        }

        internal async Task FetchConfigFile()
        {
            GetObjectRequest getObjectRequest = new GetObjectRequest
            {
                BucketName = S3EventLog.BucketName,
                Key = $"{S3EventLog.KeyName.Substring(0, S3EventLog.KeyName.LastIndexOf("/"))}/config.json"
            };
            try
            {
                using GetObjectResponse getObjectResponse = await Resources.GetInstance().S3Client.GetObjectAsync(getObjectRequest);
                using Stream responseStream = getObjectResponse.ResponseStream;
                using StreamReader reader = new StreamReader(responseStream);
                string contents = reader.ReadToEnd();
                if (string.IsNullOrEmpty(contents))
                {
                    IsConfigFileAvailableAndNotEmpty = false;
                    LogEvents.Add(Utils.CreateLogEvent(this, IRILM.ConfigFileIsEmpty));
                }
                else
                {
                    JObject jObject = JObject.Parse(contents);
                    DispatchIdAndDispatchReqApi = new DispatchIdAndDispatchReqApi
                    {
                        DispatchId = (string)jObject["DispatchId"],
                        DispatchReqApi = (string)jObject["DispatchReqApi"]
                    };
                    IsConfigFileAvailableAndNotEmpty = true;
                    LogEvents.Add(Utils.CreateLogEvent(this, IRILM.ConfigFileRetrieved));
                }
            }
            catch (Exception ex)
            {
                IsConfigFileAvailableAndNotEmpty = false;
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.ConfigFileNotRetrieved(ex)));
            }
        }

        internal async Task SplitFileInBatches()
        {
            try
            {
                int batchSize = int.Parse(Environment.GetEnvironmentVariable("BatchSize"));
                string filename = S3EventLog.KeyName;
                string displayFilename = S3EventLog.KeyName.Substring(S3EventLog.KeyName.LastIndexOf("/") + 1);
                filename = filename.Split(".")[0];
                var batchNumber = filename.Contains("$$$") ? int.Parse(filename.Split("$$$")[1]) : 0;
                filename = filename.Contains("$$$") ? filename.Split("$$$")[0] : filename;
                string completeFilename = filename;
                filename = filename.Substring(filename.LastIndexOf("/") + 1);
                var initiatorRecord = await Utils.GetInitiatorRecordByFilename(completeFilename);
                if (batchNumber == 0)
                {
                    BatchId = Guid.NewGuid().ToString();
                    int noOfBatches = DataTable.RowEntries.Count / batchSize;
                    if (DataTable.RowEntries.Count % batchSize > 0)
                        noOfBatches++;
                    await Utils.InsertInitiatorRecordByFilename(completeFilename, BatchId, noOfBatches.ToString(), displayFilename);
                    if (DataTable.RowEntries.Count > batchSize)
                    {
                        await SplitFile("1", filename, batchSize);
                        IsFileSplitted = true;
                        return;
                    }
                    else
                    {
                        IsFileSplitted = false;
                        return;
                    }
                }
                else
                {
                    if (DataTable.RowEntries.Count > batchSize)
                    {
                        await SplitFile(batchNumber.ToString(), filename, batchSize);
                        IsFileSplitted = true;
                        return;
                    }
                    else
                    {
                        BatchId = initiatorRecord.BatchId;
                        IsFileSplitted = false;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.FileSplitError(ex)));
                throw ex;
            }
        }

        public async Task SplitFile(string batchNo, string filename, int batchSize)
        {
            int timeDelay = int.Parse(Environment.GetEnvironmentVariable("TimeDelay"));
            
            string newBatchNo = (int.Parse(batchNo) + 1).ToString();
            try
            {
                DeleteObjectRequest deleteObjectRequest = new DeleteObjectRequest
                {
                    BucketName = S3EventLog.BucketName,
                    Key = S3EventLog.KeyName
                };
                DeleteObjectResponse getObjectResponse = await Resources.GetInstance().S3Client.DeleteObjectAsync(deleteObjectRequest);
                if (DataTable.Extension == "csv")
                {
                    var csv = new StringBuilder();
                    string header = string.Empty;
                    foreach (var headerValue in DataTable.Headers)
                    {
                        header += headerValue.Value + ",";
                    }
                    header = header.TrimEnd(',');
                    var batchValues = DataTable.RowEntries.Take(batchSize);
                    csv.AppendLine(header);
                    foreach (var row in batchValues)
                    {
                        List<string> tempRow = new List<string>();
                        foreach(var r in row.Values)
                        {
                            if (r.Contains(","))
                                tempRow.Add("\"" + r + "\"");
                            else
                                tempRow.Add(r);
                        }
                        var rowValue = string.Join(",", tempRow);
                        csv.AppendLine(rowValue);
                    }
                    PutObjectRequest putObject = new PutObjectRequest
                    {
                        BucketName = S3EventLog.BucketName,
                        Key = S3EventLog.KeyName.Substring(0, S3EventLog.KeyName.LastIndexOf("/") + 1) + filename + "$$$" + batchNo + ".csv",
                        ContentType = "text/csv",
                        ContentBody = csv.ToString()
                    };
                    PutObjectResponse putObjectResponse = await Resources.GetInstance().S3Client.PutObjectAsync(putObject);

                    Thread.Sleep(timeDelay * 1000);
                    csv = new StringBuilder();
                    batchValues = DataTable.RowEntries.Skip(batchSize);
                    csv.AppendLine(header);
                    foreach (var row in batchValues)
                    {
                        List<string> tempRow = new List<string>();
                        foreach (var r in row.Values)
                        {
                            if (r.Contains(","))
                                tempRow.Add("\"" + r + "\"");
                            else
                                tempRow.Add(r);
                        }
                        var rowValue = string.Join(",", tempRow);
                        csv.AppendLine(rowValue);
                    }
                    putObject = new PutObjectRequest
                    {
                        BucketName = S3EventLog.BucketName,
                        Key = S3EventLog.KeyName.Substring(0, S3EventLog.KeyName.LastIndexOf("/") + 1) + filename + "$$$" + newBatchNo + ".csv",
                        ContentType = "text/csv",
                        ContentBody = csv.ToString()
                    };
                    putObjectResponse = await Resources.GetInstance().S3Client.PutObjectAsync(putObject);
                }
                else if (DataTable.Extension == "xlsx")
                {
                    using var memoryStream = new MemoryStream();
                    {
                        var workBook = new XLWorkbook();
                        workBook.AddWorksheet();
                        var workSheet = workBook.Worksheet(1);
                        int col = 1;
                        foreach (var header in DataTable.Headers)
                        {
                            workSheet.Cell(1, col).SetValue<string>(header.Value);
                            col++;
                        }
                        int rowNo = 2;
                        foreach (var row in DataTable.RowEntries.Take(batchSize))
                        {
                            col = 1;
                            foreach (var rowValue in row.Values)
                            {
                                workSheet.Cell(rowNo, col).SetValue<string>(rowValue);
                                col++;
                            }
                            rowNo++;
                        }
                        workBook.SaveAs(memoryStream);
                        memoryStream.Position = 0;
                        PutObjectRequest putObject = new PutObjectRequest
                        {
                            BucketName = S3EventLog.BucketName,
                            Key = S3EventLog.KeyName.Substring(0, S3EventLog.KeyName.LastIndexOf("/") + 1) + filename + "$$$" + batchNo + ".xlsx",
                            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            InputStream = memoryStream
                        };
                        PutObjectResponse putObjectResponse = await Resources.GetInstance().S3Client.PutObjectAsync(putObject);
                        memoryStream.Dispose();
                    }

                    Thread.Sleep(timeDelay * 1000);
                    using var memoryStreamFile2 = new MemoryStream();
                    {
                        var workBook = new XLWorkbook();
                        workBook.AddWorksheet();
                        var workSheet = workBook.Worksheet(1);
                        int col = 1;
                        foreach (var header in DataTable.Headers)
                        {
                            workSheet.Cell(1, col).SetValue<string>(header.Value);
                            col++;
                        }
                        int rowNo = 2;
                        foreach (var row in DataTable.RowEntries.Skip(batchSize))
                        {
                            col = 1;
                            foreach (var rowValue in row.Values)
                            {
                                workSheet.Cell(rowNo, col).SetValue<string>(rowValue);
                                col++;
                            }
                            rowNo++;
                        }
                        workBook.SaveAs(memoryStreamFile2);
                        memoryStreamFile2.Position = 0;
                        PutObjectRequest putObject = new PutObjectRequest
                        {
                            BucketName = S3EventLog.BucketName,
                            Key = S3EventLog.KeyName.Substring(0, S3EventLog.KeyName.LastIndexOf("/") + 1) + filename + "$$$" + newBatchNo + ".xlsx",
                            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            InputStream = memoryStreamFile2
                        };
                        PutObjectResponse putObjectResponse = await Resources.GetInstance().S3Client.PutObjectAsync(putObject);
                        memoryStreamFile2.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        internal async Task FetchTargetFile()
        {
            GetObjectRequest getObjectRequest = new GetObjectRequest
            {
                BucketName = S3EventLog.BucketName,
                Key = S3EventLog.KeyName
            };
            try
            {
                using GetObjectResponse getObjectResponse = await Resources.GetInstance().S3Client.GetObjectAsync(getObjectRequest);
                using Stream responseStream = getObjectResponse.ResponseStream;
                DataTable = new DataTable(responseStream, S3EventLog.KeyName.Substring(S3EventLog.KeyName.LastIndexOf(".") + 1));
                if (DataTable.Headers.Count == 0 && DataTable.RowEntries.Count == 0)
                {
                    IsTargetFileAvailableAndNotEmpty = false;
                    LogEvents.Add(Utils.CreateLogEvent(this, IRILM.TargetFileIsEmpty));
                }
                else
                {
                    IsTargetFileAvailableAndNotEmpty = true;
                    LogEvents.Add(Utils.CreateLogEvent(this, IRILM.TargetFileRetrieved));
                }
            }
            catch (Exception ex)
            {
                IsTargetFileAvailableAndNotEmpty = false;
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.TargetFileNotRetrieved(ex)));
            }
        }

        internal async Task ArchiveTargetFile()
        {
            try
            {
                CopyObjectRequest copyObjectRequest = new CopyObjectRequest
                {
                    SourceBucket = S3EventLog.BucketName,
                    SourceKey = S3EventLog.KeyName,
                    DestinationBucket = S3EventLog.BucketName,
                    CannedACL = S3CannedACL.BucketOwnerFullControl,
                    DestinationKey = $"{S3EventLog.KeyName.Substring(0, S3EventLog.KeyName.LastIndexOf("/"))}" +
                                $"/archive{S3EventLog.KeyName.Substring(S3EventLog.KeyName.LastIndexOf("/"))}"
                };
                CopyObjectResponse copyObjectResponse = await Resources.GetInstance().S3Client.CopyObjectAsync(copyObjectRequest);
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.TargetFileCopied));
                try
                {
                    DeleteObjectRequest deleteObjectRequest = new DeleteObjectRequest
                    {
                        BucketName = S3EventLog.BucketName,
                        Key = S3EventLog.KeyName
                    };
                    DeleteObjectResponse deleteObjectResponse = await Resources.GetInstance().S3Client.DeleteObjectAsync(deleteObjectRequest);
                    LogEvents.Add(Utils.CreateLogEvent(this, IRILM.TargetFileDeleted));
                }
                catch (Exception ex)
                {
                    LogEvents.Add(Utils.CreateLogEvent(this, IRILM.TargetFileNotDeleted(ex)));
                }
            }
            catch (Exception ex)
            {
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.TargetFileNotCopied(ex)));
            }
        }

        internal void ValidateConfigFile()
        {
            if (DispatchIdAndDispatchReqApi == null ||
                string.IsNullOrWhiteSpace(DispatchIdAndDispatchReqApi.DispatchId) ||
                string.IsNullOrWhiteSpace(DispatchIdAndDispatchReqApi.DispatchReqApi))
            {
                IsConfigFileValid = false;
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.ConfigFileInvalidated));
            }
            else
            {
                IsConfigFileValid = true;
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.ConfigFileValidated));
            }
        }

        internal async Task FetchBearerToken()
        {
            BearerToken = await WXMService.GetLoginToken(
                Resources.GetInstance().AccountConfiguration.WXMUser,
                Resources.GetInstance().AccountConfiguration.WXMAPIKey);
            if (BearerToken == default)
            {
                IsLoginPossible = false;
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.LoginUnsuccessful));
            }
            else
            {
                IsLoginPossible = true;
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.LoginSuccessful));
            }
        }

        internal async Task ValidateDispatch()
        {
            Dispatch = await WXMService.GetDispatchById($"Bearer {BearerToken.AccessToken}", DispatchIdAndDispatchReqApi.DispatchId);
            if (Dispatch == default)
            {
                IsDispatchValid = false;
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.DispatchIdUnknown));
            }
            else if (Dispatch.IsLive == false)
            {
                IsDispatchValid = false;
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.DispatchNotLive));
            }
            else
            {
                Questions = await WXMService.GetQuestionsByQNR($"Bearer {BearerToken.AccessToken}", Dispatch.QuestionnaireName);
                if (!(Questions?.Count > 0))
                {
                    IsDispatchValid = false;
                    LogEvents.Add(Utils.CreateLogEvent(this, IRILM.DispatchHasNoQuestions));
                }
                else
                {
                    IsDispatchValid = true;
                    LogEvents.Add(Utils.CreateLogEvent(this, IRILM.DispatchAndQsFetched));
                }
            }
        }

        internal void ValidateTargetFileHeaders()
        {
            List<string> headerWiseLogMessages = new List<string>();
            Dictionary<string, string> headerToQuestionIdMapping = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (Question question in Questions)
            {
                List<string> mappedHeaderTags = question.PerLocationOverride?.FirstOrDefault(x => x.Location == Dispatch.QuestionnaireName)?.MappedHeaderTags;
                if (mappedHeaderTags == null || mappedHeaderTags.Count == 0)
                    mappedHeaderTags = question.MappedHeaderTags;
                if (mappedHeaderTags?.Count > 0)
                    mappedHeaderTags.ForEach(x =>
                    {
                        if (!headerToQuestionIdMapping.ContainsKey(x))
                            headerToQuestionIdMapping.Add(x, question.Id);
                    });
            }

            Dictionary<string, List<string>> questionIdtoHeaderMapping = new Dictionary<string, List<string>>();
            for (int i = 0; i < DataTable.Headers.Count; i++)
            {
                Header header = DataTable.Headers[i];
                if (header.Value != null && headerToQuestionIdMapping.ContainsKey(header.Value))
                {
                    header.IsValid = true;
                    string qid = headerToQuestionIdMapping[header.Value];
                    headerWiseLogMessages.Add($"Header:{header.Value} (HeaderNumber:{i + 1}) has been mapped to {qid}.");
                    if (!questionIdtoHeaderMapping.ContainsKey(qid))
                        questionIdtoHeaderMapping[qid] = new List<string> { header.Value };
                    else
                        questionIdtoHeaderMapping[qid].Add(header.Value);
                    header.Value = qid;
                }
                else
                {
                    header.IsValid = false;
                    headerWiseLogMessages.Add($"Header:{header.Value} (HeaderNumber:{i + 1}) couldn't be mapped to any XM Question for the Dispatch.");
                }
            }

            if (DataTable.Headers.All(x => !x.IsValid))
            {
                IsTargetFileHeaderWiseValid = false;
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.TargetFileHasNoValidHeaders(JsonConvert.SerializeObject(headerToQuestionIdMapping))));
            }
            else
            {
                List<KeyValuePair<string, List<string>>> duplicateHeaders = questionIdtoHeaderMapping.Where(x => x.Value.Count > 1)?.ToList();
                if (duplicateHeaders?.Count > 0)
                {
                    IsTargetFileHeaderWiseValid = false;
                    LogEvents.Add(Utils.CreateLogEvent(this, IRILM.TargetFileHasDuplicateHeaders(duplicateHeaders.Select(x => x.Value).ToList())));
                }
                else
                {
                    IsTargetFileHeaderWiseValid = true;
                    LogEvents.Add(Utils.CreateLogEvent(this, IRILM.TargetFileHasNoDuplicateHeadersAndHasSomeValidHeaders(headerWiseLogMessages, JsonConvert.SerializeObject(headerToQuestionIdMapping))));
                }
            }
        }

        internal void ValidateTargetFileRows()
        {
            int columnCount = DataTable.Headers.Count;
            List<string> rowWiseLogMessages = new List<string>();
            for (int i = 0; i < DataTable.RowEntries.Count; i++)
            {
                RowEntry rowEntry = DataTable.RowEntries[i];
                if (rowEntry.Values.Count != columnCount)
                {
                    rowEntry.IsValid = false;
                    string comparison = rowEntry.Values.Count < columnCount ? "lesser" : "greater";
                    rowWiseLogMessages.Add($"RowNumber:{i + 1} was rejected for having {comparison} than required values in comparison to the file's headers.");
                }
                else
                {
                    rowEntry.IsValid = true;
                }
            }
            if (DataTable.RowEntries.All(x => !x.IsValid))
            {
                IsTargetFileRowWiseValid = false;
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.TargetFileHasNoValidRows));
            }
            else
            {
                IsTargetFileRowWiseValid = true;
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.TargetFileHasSomeValidRows(rowWiseLogMessages)));
            }
        }

        internal async Task RequestDispatch()
        {
            List<List<PreFillValue>> preFillValuesForMultipleTokens = new List<List<PreFillValue>>();
            foreach (var rowEntry in DataTable.RowEntries.Where(x => x.IsValid))
            {
                List<PreFillValue> preFillValuesForSingleToken = new List<PreFillValue>();
                for (int i = 0; i < rowEntry.Values.Count; i++)
                {
                    string input = rowEntry.Values[i];
                    string questionId = DataTable.Headers[i].Value;
                    bool isHeaderValid = DataTable.Headers[i].IsValid;
                    if (isHeaderValid && !string.IsNullOrWhiteSpace(input))
                    {
                        preFillValuesForSingleToken.Add(new PreFillValue { Input = input, QuestionId = questionId });
                    }
                }
                preFillValuesForMultipleTokens.Add(preFillValuesForSingleToken);
            }

            HttpClient httpClient = new HttpClient();
            string requestUri = $"{DispatchIdAndDispatchReqApi.DispatchReqApi}/api/DispatchRequest";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Add("Authorization", $"Bearer {BearerToken.AccessToken}");
            request.Headers.Add("BatchID", BatchId);
            List<DispatchRequest> dispatchRequests = new List<DispatchRequest>
                {
                    new DispatchRequest
                    {
                        DispatchID = DispatchIdAndDispatchReqApi.DispatchId,
                        PreFill = preFillValuesForMultipleTokens
                    }
                };
            request.Content = new StringContent(JsonConvert.SerializeObject(dispatchRequests), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await httpClient.SendAsync(request);
            string httpResponseBodyAsString = await response.Content?.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                BatchResponse batchResponse = JsonConvert.DeserializeObject<BatchResponse>(httpResponseBodyAsString);
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.RequestForDispatchWasAccepted($"{((int)response.StatusCode).ToString()}-" +
                    $"{response.StatusCode}-{httpResponseBodyAsString}"), batchResponse.BatchId));
            }
            else
            {
                LogEvents.Add(Utils.CreateLogEvent(this, IRILM.RequestForDispatchWasRejected($"{((int)response.StatusCode).ToString()}-" +
                    $"{response.StatusCode}-{httpResponseBodyAsString}")));
            }
        }
    }

    internal class DispatchIdAndDispatchReqApi
    {
        internal string DispatchId { get; set; }
        internal string DispatchReqApi { get; set; }
    }

    internal class DataTable
    {
        [JsonProperty]
        internal string Extension { get; set; }
        [JsonProperty]
        internal List<Header> Headers { get; set; } = new List<Header>();
        [JsonProperty]
        internal List<RowEntry> RowEntries { get; set; } = new List<RowEntry>();

        public DataTable(Stream stream, string extension)
        {
            Extension = extension;
            if (Extension == "csv")
            {
                using StreamReader streamReader = new StreamReader(stream);
                int count = 0;
                while (!streamReader.EndOfStream)
                {
                    string line = streamReader.ReadLine();
                    //check for blank rows
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string[] cells = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                        cells = RemoveQoutes(cells);
                        //check for rows with all empty values
                        if (cells.Any(x => !string.IsNullOrEmpty(x)))
                        {
                            if (count == 0)
                                cells.ToList().ForEach(x => Headers.Add(new Header(x)));
                            else
                                RowEntries.Add(new RowEntry(cells));
                            count++;
                        }
                    }
                }
            }
            else if (Extension == "xlsx")
            {
                var workBook = new XLWorkbook(stream);

                //only the first sheet will be processed
                var workSheet = workBook.Worksheet(1);

                int? startColumnNumber = workSheet?.FirstCellUsed()?.Address?.ColumnNumber;
                int? startRowNumber = workSheet?.FirstCellUsed()?.Address?.RowNumber;
                int? endColumnNumber = workSheet?.LastCellUsed()?.Address?.ColumnNumber;
                int? endRowNumber = workSheet?.LastCellUsed()?.Address?.RowNumber;

                if (startColumnNumber == null || startRowNumber == null || endColumnNumber == null || endRowNumber == null)
                    return;

                for (int y = startRowNumber.Value; y <= endRowNumber.Value; y++)
                {
                    //check for null rows
                    if (workSheet.Row(y).IsEmpty())
                        continue;

                    List<string> rowValues = new List<string>();
                    for (int x = startColumnNumber.Value; x <= endColumnNumber.Value; x++)
                    {
                        string cellValue = workSheet.Cell(y, x).GetValue<string>();
                        //headers
                        if (y == startRowNumber.Value)
                            Headers.Add(new Header(cellValue));
                        else
                            rowValues.Add(cellValue);
                    }
                    if (y != startRowNumber.Value)
                        RowEntries.Add(new RowEntry(rowValues));
                }
            }
        }

        public string[] RemoveQoutes(string[] cells)
        {
            string[] newcells = new string[cells.Length];
            int index = 0;
            foreach (var cell in cells)
            {
                string temp = cell;
                if (!string.IsNullOrEmpty(temp))
                {
                    if (temp.StartsWith("\""))
                        temp = temp.Substring(1);
                    if (temp.EndsWith("\""))
                        temp = temp.Remove(temp.Length - 1);
                }
                newcells[index++] = temp;
            }
            return newcells;
        }
    }

    internal class Header
    {
        [JsonProperty]
        internal bool IsValid { get; set; }
        [JsonProperty]
        internal string Value { get; set; }

        public Header(string value)
        {
            IsValid = false;
            Value = value.Trim(); //Trimming Whitespace
        }
    }

    internal class RowEntry
    {
        [JsonProperty]
        internal bool IsValid { get; set; }
        [JsonProperty]
        internal List<string> Values { get; set; }

        public RowEntry(IEnumerable<string> values)
        {
            IsValid = false;
            Values = new List<string>(values.Select(x => x.Trim())); //Trimming Whitespace
        }
    }
}
