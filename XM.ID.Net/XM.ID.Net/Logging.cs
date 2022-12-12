using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace XM.ID.Net
{
    /// <summary>
    /// Stores details about an unit event in the entire 
    /// Partner-Hosted Invitation-Delivery workflow of a
    /// DispatchAPI Request
    /// </summary>
    [BsonIgnoreExtraElements]
    public class LogEvent
    {
        /// <summary>
        /// Unique MongoDB Document Identifier
        /// </summary>
        [BsonId]
        public string Id { get; set; }
        /// <summary>
        /// Token Number
        /// </summary>
        public string TokenId { get; set; }
        /// <summary>
        /// Unique Delivery-Plan Identifier
        /// </summary>
        public string DeliveryWorkFlowId { get; set; }
        /// <summary>
        /// Unique Dispatch Identifier
        /// </summary>
        public string DispatchId { get; set; }
        /// <summary>
        /// Unique Batch Identifier
        /// </summary>
        public string BatchId { get; set; }
        /// <summary>
        /// Account Name
        /// </summary>
        public string User { get; set; }
        /// <summary>
        /// Unique Questinnaire Identifier
        /// </summary>
        public string Location { get; set; }
        /// <summary>
        /// Object creation date-time stamp
        /// </summary>
        public DateTime Created { get; set; }
        /// <summary>
        /// Object modification date-time stamp
        /// </summary>
        public DateTime Updated { get; set; }
        /// <summary>
        /// Captures the Journey Details of the Invitation
        /// </summary>
        public List<InvitationLogEvent> Events { get; set; }
        /// <summary>
        /// Recepient's UUID (Actual)
        /// </summary>
        public string Target { get; set; }
        /// <summary>
        /// Recepient's UUID (Hashed/Actual)
        /// </summary>
        public string TargetHashed { get; set; }
        /// <summary>
        /// Pre-Fill Questions and Answers
        /// </summary>
        public List<Prefill> Prefills { get; set; }
        /// <summary>
        /// Details about the Event
        /// </summary>
        public LogMessage LogMessage { get; set; }
        /// <summary>
        /// Additional LogEvent Identifiers
        /// </summary>
        public List<string> Tags { get; set; }
        /// <summary>
        /// Describes the Notification Status of the Event.
        /// Have the subscribers been notified or not.
        /// </summary>
        public bool IsNotified { get; set; }
        /// <summary>
        /// Describes the S3Event that triggered the Initiator Component
        /// </summary>
        public S3EventLog S3EventLog { get; set; }
    }

    /// <summary>
    /// Stores details about the triggered S3Event
    /// </summary>
    public class S3EventLog
    {
        /// <summary>
        /// Type of S3Event
        /// </summary>
        public string EventName { get; set; }
        /// <summary>
        /// Date-time stamp of when the S3Event was fired.
        /// </summary>
        public DateTime EventTime { get; set; }
        /// <summary>
        /// S3Bucket from where the S3Event originated.
        /// </summary>
        public string BucketName { get; set; }
        /// <summary>
        /// S3File that triggered the S3Event
        /// </summary>
        public string KeyName { get; set; }
    }

    /// <summary>
    /// Describes the Api-Level prefills of a Token
    /// </summary>
    [BsonIgnoreExtraElements]
    public class Prefill
    {
        /// <summary>
        /// Unique Question Identifier
        /// </summary>
        public string QuestionId { get; set; }
        /// <summary>
        /// Actual Answer Value
        /// </summary>
        public string Input { get; set; }
        /// <summary>
        /// Hashed (if opted for in XM) Answer Value
        /// </summary>
        public string Input_Hash { get; set; }
    }

    /// <summary>
    /// Describes an Log-Event in terms of a message, level and an exception (if any)
    /// </summary>
    [BsonIgnoreExtraElements]
    public class LogMessage
    {
        /// <summary>
        /// Log Details
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// Log Level
        /// </summary>
        public string Level { get; set; }
        /// <summary>
        /// Exception Details
        /// </summary>
        public string Exception { get; set; }

        public const string SeverityLevel_Verbose = "Debug";
        public const string SeverityLevel_Warning = "Warning";
        public const string SeverityLevel_Information = "Information";
        public const string SeverityLevel_Error = "Error";
        public const string SeverityLevel_Critical = "Failure";

        public bool IsLogInsertible(int appLogLevel)
        {
            if (Message == null)
                return false;

            int logMessageLevel = Level switch
            {
                SeverityLevel_Critical => 1,
                SeverityLevel_Error => 2,
                SeverityLevel_Warning => 3,
                SeverityLevel_Information => 4,
                SeverityLevel_Verbose => 5,
                _ => 10
            };

            return logMessageLevel <= appLogLevel;
        }
    }

    /// <summary>
    /// Stores details about an unit event in the entire
    /// Partner-Hosted Invitation-Delivery workflow of a
    /// Survey Token
    /// </summary>
    [BsonIgnoreExtraElements]
    public class InvitationLogEvent
    {
        /// <summary>
        /// Object creation date-time stamp
        /// </summary>
        public DateTime TimeStamp { get; set; }
        /// <summary>
        /// Restricted to "Email"/"SMS"/"Unknown"/"Invalid" for Dispatcher Component
        /// </summary>
        public EventChannel Channel { get; set; }
        /// <summary>
        /// Restricted to "DispatchSuccessful"/"DispatchUnsuccessful" for Dispatcher Component
        /// </summary>
        public EventAction Action { get; set; }
        /// <summary>
        /// Field to store any additional details
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// Recepient's Email-Id/Mobile-Number
        /// </summary>
        public string TargetId { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public DeliveryEventStatus EventStatus { get; set; }
        /// <summary>
        /// details about the event
        /// </summary>
        public LogMessage LogMessage { get; set; }
    }

    /// <summary>
    /// For Dispatcher Component, value used should be "Email"/"SMS"/"Unknown"/"Invalid"
    /// </summary>
    public enum EventChannel { Email, SMS, DispatchAPI, Unknown, Invalid };

    /// <summary>
    /// For Dispatcher Component, value used should be "DispatchSuccessful"/"DispatchUnsuccessful"
    /// </summary>
    public enum EventAction
    {
        Requested, Rejected, TokenCreated, Sent, Error, Supressed,
        DispatchSuccessful, DispatchUnsuccessful, Throttled
    };

    [BsonIgnoreExtraElements]
    public class DeliveryEventStatus
    {
        public int Requested { get; set; }
        public int Accepetd { get; set; }
        public int Rejected { get; set; }
    }

    /// <summary>
    /// Invitation-Related Dispatcher Log-Messages (IRDLM)
    /// </summary>
    public static class IRDLM
    {
        public static LogMessage Dequeued = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "Dequeued"
        };

        public static LogMessage Validated(string additionalParams)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Verbose,
                Message = $"Validated (Additional Token Parameters: {additionalParams})"
            };
        }

        public static LogMessage Invalidated = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "Few of the mandatory prefills i.e. Token ID, Batch ID or Dispatch ID are not properly configured in Webex Experience Management account. " +
            "Please check the questionnaire and prefills for this dispatch in Webex Experience Management account."
        };

        public static LogMessage ChannelNotConfigured1 = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "Channel couldn't be inferred as both email ID and mobile number are available"
        };

        public static LogMessage ChannelNotConfigured2 = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "Channel couldn't be inferred as both email ID and mobile number are not available"
        };

        public static LogMessage EmailChannelConfigured = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "Channel inferred as Email"
        };

        public static LogMessage SmsChannelConfigured = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "Channel inferred as SMS"
        };

        public static LogMessage UserDataFound(string id)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Verbose,
                Message = $"Corresponding Event log was found (id: {id})"
            };
        }

        public static LogMessage UserDataNotFound = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "Invites were not sent for this record. Data for substitutes used in message template could not be fetched. Please refer the troubleshooting guide below to check for possible reasons for this issue." +
            "\n https://xm.webex.com/docs/cxsetup/guides/trouble-shooting/."
        };

        public static LogMessage HashLookUpDictConfigured = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "Corresponding Hash Look-Up Dictionary has been configured"
        };

        public static LogMessage DispatchVendorNamePresent(string name)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Verbose,
                Message = $"Corresponding Dispatch's Vendor Name is found (name: {name})"
            };
        }

        public static LogMessage DispatchVendorNameMissing = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "Corresponding Dispatch's Vendor Name is missing from the Account Configuration Module"
        };

        public static LogMessage DispatchVendorConfigPresent(Vendor vendor)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Verbose,
                Message = $"Corresponding Vendor Details are available (vendor details: {JsonConvert.SerializeObject(vendor)})"
            };
        }

        public static LogMessage DispatchVendorConfigMissing = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "Corresponding Vendor Details are missing from the Account Configuration Module"
        };

        public static LogMessage VendorIsBulk = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "Corresponding Vendor is of type Bulk-Send. Invitation will now be inserted into the database."
        };

        public static LogMessage VendorIsNotBulk = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "Corresponding Vendor is of type Single-Send. Invitation will now be prepared for dispatched."
        };

        public static LogMessage DispatchVendorImplemenatationPresent(Vendor vendor)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Verbose,
                Message = $"Corresponding Vendor implemetation object was found in the serverless compute's memory (vendor details: {JsonConvert.SerializeObject(vendor)})"
            };
        }

        public static LogMessage DispatchVendorImplementationMissing = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "Corresponding channel vendor configuration was not found. Please configure channel vendor details as per the guide below. " +
            "\n https://xm.webex.com/docs/cxsetup/guides/acm/"
        };

        /// <summary>
        /// Create a LogMessage which marks an Invitation's Dispatch status as Successful
        /// </summary>
        /// <param name="vendorName"></param>
        /// <returns>A LogMessage which marks the Invitation's Dispatch status as Successful</returns>
        public static LogMessage DispatchSuccessful(string vendorName)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Information,
                Message = $"Successfully Dispatched (via: {vendorName})"
            };
        }

        /// <summary>
        /// Create a LogMessage which marks an Invitation's Dispatch status as Unsuccessful
        /// </summary>
        /// <param name="vendorName"></param>
        /// <param name="ex">Reason for Failure</param>
        /// <returns>A LogMessage which marks the Invitation's Dispatch status as Unsuccessful due to Exception:ex</returns>
        public static LogMessage DispatchUnsuccessful(string vendorName, Exception ex)
        {
            return new LogMessage
            {
                Exception = JsonConvert.SerializeObject(ex),
                Level = LogMessage.SeverityLevel_Error,
                Message = $"Invites were not sent due to vendor API resulting into error. Please check with the respective vendor with exact error message " +
                $"received. \n https://xm.webex.com/docs/cxsetup/guides/acm/" +
                $" (via: {vendorName})"
            };
        }

        public static LogMessage ReadFromDB(string vendorName)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Verbose,
                Message = $"Read from database into memory (Bulk-Send Vendor: {vendorName})"
            };
        }

        public static LogMessage InternalException(Exception ex)
        {
            return new LogMessage
            {
                Exception = JsonConvert.SerializeObject(ex),
                Level = LogMessage.SeverityLevel_Critical,
                Message = "Error faced by the server while processing the records. Please refer the troubleshooting guide below to check for possible reasons for this issue." +
                "\n https://xm.webex.com/docs/cxsetup/guides/trouble-shooting/"
            };
        }

        public static LogMessage TimeTriggerStart = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = $"Time Trigger Serverless Compute has now started"
        };

        public static LogMessage TimeTriggerEnd(int messageCount)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Verbose,
                Message = $"Time Trigger Serverless Compute has now ended (Invitations Processed = {messageCount})"
            };
        }

        public static LogMessage TimeTriggerRunningLate = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Warning,
            Message = $"Time Trigger Serverless Compute is running late"
        };

        public static LogMessage DispatchChannelNotFound = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "Corresponding Dispatch was not found in the Account Configuration Module"
        };
    }

    /// <summary>
    /// Invitation-Related Initiator Log-Messages (IRILM)
    /// </summary>
    public static class IRILM
    {
        public static LogMessage S3EventReceived = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "A request to initiate a dispatch was received from the S3Bucket"
        };

        public static LogMessage InvalidTargetFileUploadDirectory = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "The dispatch cannot be initiated as the uploaded file wasn't uploaded into a directory as required"
        };

        public static LogMessage ConfigFileNotRetrieved(Exception ex)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Error,
                Message = $"The required config.json couldn't be retrieved from the S3Bucket. Reason => {JsonConvert.SerializeObject(ex)}"
            };
        }

        public static LogMessage ConfigFileIsEmpty = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = $"The retrieved config.json was Empty"
        };

        public static LogMessage ConfigFileRetrieved = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "The required config.json was successfully retrieved from the S3Bucket"
        };

        public static LogMessage FileSplitError(Exception ex)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Error,
                Message = $"The uploaded file couldn't be split in batches of required size. Reason => {JsonConvert.SerializeObject(ex)}"
            };
        }

        public static LogMessage TargetFileNotRetrieved(Exception ex)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Error,
                Message = $"The uploaded file couldn't be retrieved from the S3Bucket. Reason => {JsonConvert.SerializeObject(ex)}"
            };
        }

        public static LogMessage TargetFileIsEmpty = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = $"The retrieved uploaded file was Empty"
        };

        public static LogMessage TargetFileRetrieved = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "The uploaded file was successfully retrieved from the S3Bucket"
        };

        public static LogMessage ConfigFileInvalidated = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "The required config.json hasn't been configured correctly. Please check again!"
        };

        public static LogMessage ConfigFileValidated = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "The required config.json has been configured correctly"
        };

        public static LogMessage LoginUnsuccessful = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "The login into WXM failed. As a result, a Bearer-Token couldn't be fetched"
        };

        public static LogMessage LoginSuccessful = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "The login into WXM succeeded. As a result, a Bearer-Token was fetched"
        };

        public static LogMessage DispatchIdUnknown = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "The config.json specifies an unknown Dispatch-Id"
        };

        public static LogMessage DispatchNotLive = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "The config.json specifies a Not-Live Dispatch"
        };

        public static LogMessage DispatchHasNoQuestions = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "The config.json specifies a Dispatch with no associated Questions"
        };

        public static LogMessage DispatchAndQsFetched = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "The required Dispatch details, along with its Questions, were successfully fetched from XM"
        };

        public static LogMessage TargetFileHasNoValidHeaders(string headersAvailable)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Error,
                Message = $"The uploaded file cannot be processed as none of the headers could be mapped to" +
                $" a Question-Id that belongs to the corresponding Dispatch. Available Headers in XM for the corresponding Dispatch: {headersAvailable}"
            };
        }

        public static LogMessage TargetFileHasDuplicateHeaders(List<List<string>> duplicateHeaders)
        {
            StringBuilder duplicateHeaderDetails = new StringBuilder();
            foreach (List<string> duplicateSet in duplicateHeaders)
                duplicateHeaderDetails.Append($"({string.Join(", ", duplicateSet)}); ");
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Error,
                Message = $"The uploaded file cannot be processed as it has the following duplicate headers: {duplicateHeaderDetails}."
            };
        }

        public static LogMessage TargetFileHasNoDuplicateHeadersAndHasSomeValidHeaders(List<string> headerWiseLogMessages, string headersAvailable)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Information,
                Message = $"The uploaded file's headers are ready for processing." +
                $" Details => {string.Join(" ", headerWiseLogMessages)} Available Headers in XM for the corresponding Dispatch: {headersAvailable}"
            };
        }

        public static LogMessage TargetFileHasNoValidRows = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "No invites were sent as all records in the uploaded data file had errors. Please check the file again and upload in the correct format and process again."
        };

        public static LogMessage TargetFileHasSomeValidRows(List<string> rowWiseLogMessages)
        {
            string details = rowWiseLogMessages.Count == 0 ? "All rows were accepted." : string.Join(" ", rowWiseLogMessages);
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Information,
                Message = $"The uploaded file's rows are ready for processing. Details => {details}"
            };
        }

        public static LogMessage RequestForDispatchWasRejected(string httpResponseAsString)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Error,
                Message = httpResponseAsString
            };
        }

        public static LogMessage RequestForDispatchWasAccepted(string httpResponseAsString)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Information,
                Message = httpResponseAsString
            };
        }

        public static LogMessage TargetFileNotCopied(Exception ex)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Warning,
                Message = $"The uploaded file couldn't be copied to the desired Archive folder. Reason => {JsonConvert.SerializeObject(ex)}"
            };
        }

        public static LogMessage TargetFileCopied = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "The uploaded file was successfully copied to the desired Archive folder"
        };

        public static LogMessage TargetFileNotDeleted(Exception ex)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Warning,
                Message = $"The uploaded file couldn't be deleted from the S3Bucket. Reason => {JsonConvert.SerializeObject(ex)}"
            };
        }

        public static LogMessage TargetFileDeleted = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "The uploaded file was successfully deleted from the S3Bucket"
        };

        public static LogMessage InternalException(Exception ex)
        {
            return new LogMessage
            {
                Exception = JsonConvert.SerializeObject(ex),
                Level = LogMessage.SeverityLevel_Critical,
                Message = "Error faced by the server while processing the records. Please refer the troubleshooting guide below to check for possible reasons for this issue." +
                "\n https://xm.webex.com/docs/cxsetup/guides/trouble-shooting/"
            };
        }
    }

    /// <summary>
    /// S3 Sync related Log-Messages (SSLM)
    /// </summary>
    public static class SSLM
    {
        public static List<LogEvent> logs = new List<LogEvent>();

        public static LogMessage LocalSync = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "Local to S3 Sync completed"
        };

        public static LogMessage SFTPSync = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Verbose,
            Message = "Sftp to S3 Sync completed"
        };

        public static LogMessage NoConnectorFound = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "Select proper Connector"
        };

        public static LogMessage ApplicatonStopped = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Critical,
            Message = "The Application was stopped"
        };

        public static LogMessage Connector(string connectorType)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Verbose,
                Message = $"Connector Selected : {connectorType})"
            };
        }

        public static LogMessage Invalidated = new LogMessage
        {
            Level = LogMessage.SeverityLevel_Error,
            Message = "Few of the mandatory prefills i.e. Token ID, Batch ID or Dispatch ID are not properly configured in Webex Experience Management account. " +
            "Please check the questionnaire and prefills for this dispatch in Webex Experience Management account."
        };

        /// <summary>
        /// Create a LogMessage which marks an Invitation's Dispatch status as Unsuccessful
        /// </summary>
        /// <param name="vendorName"></param>
        /// <param name="ex">Reason for Failure</param>
        /// <returns>A LogMessage which marks the Invitation's Dispatch status as Unsuccessful due to Exception:ex</returns>
        public static LogMessage SFTPConnectivityUnsuccessful(Exception ex)
        {
            return new LogMessage
            {
                Exception = JsonConvert.SerializeObject(ex),
                Level = LogMessage.SeverityLevel_Error,
                Message = $"Failed at connecting SFTP (Error Message: {ex.Message})"
            };
        }

        public static LogMessage InternalException(Exception ex)
        {
            return new LogMessage
            {
                Exception = JsonConvert.SerializeObject(ex),
                Level = LogMessage.SeverityLevel_Critical,
                Message = "Error faced by the server while processing the records. Please refer the troubleshooting guide below to check for possible reasons for this issue." +
                "\n https://xm.webex.com/docs/cxsetup/guides/trouble-shooting/"
            };
        }

        public static LogMessage FileInfo(string filename)
        {
            return new LogMessage
            {
                Level = LogMessage.SeverityLevel_Verbose,
                Message = $"Upload of File {filename} completed"
            };
        }

    }
}
