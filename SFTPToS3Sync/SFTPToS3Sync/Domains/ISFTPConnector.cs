using System;
using System.Collections.Generic;
using System.Text;

namespace SFTPToS3Sync.Domains
{
    interface ISFTPConnector
    {
        void DownloadContent();
    }
}
