using System;
using System.Collections.Generic;
using System.Linq;
using XM.ID.Dispatcher.Net.DispatchVendors;
using XM.ID.Net;

namespace XM.ID.Dispatcher.Net
{
    internal class SingleDispatch
    {
        public MessagePayload MessagePayload { get; set; }
        public bool IsDispatcConfigured { get; set; }
        public ISingleDispatchVendor DispatchReadyVendor { get; set; }

        public void ConfigureDispatchVendor()
        {
            Vendor vendor = MessagePayload.Vendor;
            bool isDispatchVendorStrategyPresent = Resources.GetInstance().DispatchReadyVendor_CreationStrategies
                .Any(x => vendor.VendorName.StartsWith(x.Key, StringComparison.InvariantCultureIgnoreCase));
            if (isDispatchVendorStrategyPresent == false)
            {
                IsDispatcConfigured = false;
                MessagePayload.LogEvents.Add(Utils.CreateLogEvent(MessagePayload.QueueData, IRDLM.DispatchVendorImplementationMissing));
                MessagePayload.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful,
                    MessagePayload.IsEmailDelivery.Value ? EventChannel.Email : EventChannel.SMS,
                    MessagePayload.QueueData, IRDLM.DispatchVendorImplementationMissing));
            }
            else
            {
                DispatchReadyVendor = (ISingleDispatchVendor)Resources.GetInstance().DispatchReadyVendor_CreationStrategies
                    .First(x => vendor.VendorName.StartsWith(x.Key, StringComparison.InvariantCultureIgnoreCase)).Value();
                vendor.VendorDetails = vendor.VendorDetails.ToDictionary(x => x.Key, x => x.Value, StringComparer.InvariantCultureIgnoreCase);
                DispatchReadyVendor.Setup(vendor);

                IsDispatcConfigured = true;
                MessagePayload.LogEvents.Add(Utils.CreateLogEvent(MessagePayload.QueueData, IRDLM.DispatchVendorImplemenatationPresent(DispatchReadyVendor.Vendor)));
            }
        }
    }

    internal class BulkDispatch
    {
        public List<MessagePayload> MessagePayloads { get; set; }
        public bool IsDispatchConfigured { get; set; }
        public IBulkDispatchVendor DispatchReadyVendor { get; set; }

        public void ConfigureDispatchVendor()
        {
            Vendor vendor = MessagePayloads.ElementAt(0).Vendor;
            bool isDispatchVendorStrategyPresent = Resources.GetInstance().DispatchReadyVendor_CreationStrategies
                .Any(x => vendor.VendorName.StartsWith(x.Key, StringComparison.InvariantCultureIgnoreCase));
            if (isDispatchVendorStrategyPresent == false)
            {
                IsDispatchConfigured = false;
                MessagePayloads.ForEach(x => x.LogEvents.Add(Utils.CreateLogEvent(x.QueueData, IRDLM.DispatchVendorImplementationMissing)));
                MessagePayloads.ForEach(x => x.InvitationLogEvents.Add(Utils.CreateInvitationLogEvent(EventAction.DispatchUnsuccessful,
                    x.IsEmailDelivery.Value ? EventChannel.Email : EventChannel.SMS, x.QueueData, IRDLM.DispatchVendorImplementationMissing)));
            }
            else
            {
                DispatchReadyVendor = (IBulkDispatchVendor)Resources.GetInstance().DispatchReadyVendor_CreationStrategies
                    .First(x => vendor.VendorName.StartsWith(x.Key, StringComparison.InvariantCultureIgnoreCase)).Value();
                vendor.VendorDetails = vendor.VendorDetails.ToDictionary(x => x.Key, x => x.Value, StringComparer.InvariantCultureIgnoreCase);
                DispatchReadyVendor.Setup(vendor);
                IsDispatchConfigured = true;
                MessagePayloads.ForEach(x => x.LogEvents.Add(Utils.CreateLogEvent(x.QueueData, IRDLM.DispatchVendorImplemenatationPresent(vendor))));
            }
        }
    }
}
