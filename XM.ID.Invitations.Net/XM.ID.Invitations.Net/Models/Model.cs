using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Invitations.Net
{
    #region Partner hosted Models

    public interface ISampler
    {
        //inplace sampler
        public Task IsSampledAsync(List<DispatchRequest> dispatchRequests);
    }

    public class WXMSampler : ISampler
    {
        public async Task IsSampledAsync(List<DispatchRequest> dispatchRequests)
        {
            await Task.CompletedTask;
        }
    }

    public interface IUnsubscribeChecker
    {
        public Task<bool> IsUnsubscribedAsync(string customerIdentifier);
    }

    public class WXMUnsubscribeChecker : IUnsubscribeChecker
    {
        public async Task<bool> IsUnsubscribedAsync(string customIdentifier)
        {
            return await Task.FromResult(false);
        }
    }

    public interface IBatchingQueue<T>
    {
        public void Insert(T item);
    }
    #endregion

    #region BulkToken
    public class RequestBulkToken
    {
        public string DispatchId { get; set; }
        public List<List<Response>> PrefillReponse { get; set; }
        public string UUID { get; set; }
        public string Batchid { get; set; }
    }

    public class BulkTokenResult
    {
        public string Token { get; set; }
        public string UUID { get; set; }
        public string Batchid { get; set; }
    }

    public class ActivityFilter
    {
        public string BatchId { get; set; }
        public string DispatchId { get; set; }
        public string Token { get; set; }
        public string Created { get; set; }
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public string UUID { get; set; }
    }

    //public class Response
    //{
    //    /// <summary>
    //    /// Question ID of Presented Question
    //    /// </summary>
    //    [Required]
    //    public string QuestionId { get; set; }
    //    /// <summary>
    //    /// Question Text as When Presented
    //    /// </summary>
    //    public string QuestionText { get; set; }
    //    /// <summary>
    //    /// Text Input If Question Accepts Text
    //    /// </summary>
    //    public string TextInput { get; set; }
    //    /// <summary>
    //    /// Text Input If Question Accepts Number
    //    /// </summary>
    //    public int NumberInput { get; set; }

    //}
    #endregion

    #region UnSubscribe
    public class Unsubscribed
    {
        [BsonId]
        public string Id { get; set; }
        public string Email { get; set; }
        public string Mobile { get; set; }
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime UnsubscribedAt { get; set; }
    }
    #endregion

    #region SurveyQuestionnaire
    public class SurveyQuestionnaire
    {
        public bool IsNameImmutable { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string HashPIIBy { get; set; }
    }
    #endregion

    #region Dispatcher Related
    public class DB_MessagePayload
    {
        [BsonId]
        public string Id { get; set; }
        public string BulkVendorName { get; set; }
        public string Status { get; set; }
        public DateTime InsertTime { get; set; }
        public string MessagePayload { get; set; }
    }
    #endregion
}