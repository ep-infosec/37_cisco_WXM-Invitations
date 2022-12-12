using System.Collections.Generic;
using System.Threading.Tasks;
using XM.ID.Net;

namespace XM.ID.Dispatcher.Net.DispatchVendors
{
    /// <summary>
    /// Base interface for a Vendor-Implementation
    /// </summary>
    public interface IDispatchVendor
    {
        /// <summary>
        /// Dispatch's Vendor Details
        /// </summary>
        public Vendor Vendor { get; set; }
        /// <summary>
        /// Method to setup Dispatch's Vendor Details from the provided Vendor Details found in the Account-Configuration 
        /// </summary>
        /// <param name="vendor"></param>
        public void Setup(Vendor vendor);
    }

    /// <summary>
    /// Interface for a Single-Send Vendor
    /// </summary>
    public interface ISingleDispatchVendor : IDispatchVendor
    {
        /// <summary>
        /// Method to perform a Single-Send Dispatch for the provided Message-Payload and to store relevant logs
        /// </summary>
        /// <param name="messagePayload"></param>
        /// <returns></returns>
        public Task RunAsync(MessagePayload messagePayload);
    }

    /// <summary>
    /// Interface for a Bulk-Send Vendor
    /// </summary>
    public interface IBulkDispatchVendor : IDispatchVendor
    {
        /// <summary>
        /// Method to perform a Bulk-Send Dispatch for the provided Message-Payloads and to store relevant logs
        /// </summary>
        /// <param name="messagePayloads"></param>
        /// <returns></returns>
        public Task RunAsync(List<MessagePayload> messagePayloads);
    }
}
