using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using XM.ID.Net;

namespace XM.ID.Invitations.Net
{
    public class PayloadValidation
    {
        private readonly IConfiguration _config;
        List<string> dispatchArr;
        readonly ViaMongoDB viaMongo;

        public PayloadValidation(IConfiguration config, ViaMongoDB viaMongoDB)
        {
            _config = config;
            viaMongo = viaMongoDB;
        }

        public bool ValidateRequestPayloadSize(List<DispatchRequest> batchRequest, EventLogList eventLogList)
        {
            dispatchArr = new List<string>();
            double totalRecords = 0;
            
            for (int iter = 0; iter < batchRequest.Count(); iter++)
            {
                totalRecords += batchRequest[iter].PreFill.Count();
                if(!dispatchArr.Contains(batchRequest[iter].DispatchID))
                {
                    dispatchArr.Add(batchRequest[iter].DispatchID);
                }
            }

            int MaxPayload = int.Parse(_config["MaxPayloadSize"]);
            int MaxDispatch = int.Parse(_config["MaxDispatchIDCount"]);

            if (totalRecords > MaxPayload)
            {
                eventLogList.AddEventByLevel(2, $"{SharedSettings.GetMaxRecordSizeExceedeed(MaxPayload)} - {totalRecords}" , null, null); 
                return false;
            }
            else if (dispatchArr.Count > MaxDispatch)
            {
                eventLogList.AddEventByLevel(2, $"{SharedSettings.GetMaxDispatchNumberExceeded(MaxDispatch)} - {dispatchArr.Count}", null, null);
                return false;
            }
            return true;
        }

    }
}
