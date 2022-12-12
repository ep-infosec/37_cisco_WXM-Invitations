using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DPReporting
{
    class ReportFileManagement
    {
        ApplicationLog log;
        string path;

        public ReportFileManagement(string ReportPath, ApplicationLog applog)
        {
            path = ReportPath;
            log = applog;
        }

        public string SaveReportFile(byte[] e, DateTime afterdate, DateTime beforedate, int timeoffset)
        {
            if (e == null)
                return null;

            try
            {
                try
                {
                    if (!Directory.Exists(path))
                    {
                        DirectoryInfo logpath = System.IO.Directory.CreateDirectory(path);
                        path = logpath.FullName;
                    }
                }
                catch (Exception ex)
                {
                    path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "report");
                }

                var fileName = $"{"DPReport-"}{afterdate.ToString("yyyyMMdd")}{"-"}{beforedate.ToString("yyyyMMdd")}{"at"}{DateTime.UtcNow.AddMinutes(timeoffset).ToString("yyyyMMddHHmmss")}.xlsx";

                string fullFilepath = Path.Combine(path, fileName);

                File.WriteAllBytes(fullFilepath, e);

                return fileName;
            }
            catch (Exception ex0)
            {
                log.logMessage += $"{ex0.Message}   {ex0.StackTrace}\n";
                return null;
            }

        }
    }
}
