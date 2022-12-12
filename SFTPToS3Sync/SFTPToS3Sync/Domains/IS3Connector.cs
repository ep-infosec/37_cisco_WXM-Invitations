using System;
using System.Collections.Generic;
using System.Text;

namespace SFTPToS3Sync.Domains
{
    interface IS3Connector
    {
        bool UploadContent(string sOutputFile, string directoryName);
    }
}
