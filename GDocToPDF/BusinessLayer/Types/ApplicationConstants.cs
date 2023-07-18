using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// ReSharper disable IdentifierTypo

namespace GDocToPDF.BusinessLayer.Types
{
    public class ApplicationConstants
    {
        public static string ApplicationName = "RM Google Drive PDF Downloader";
        public static string ApplicationVersion = "v0.9";
        public static string ApplicationPressAnyKey = "Press any key...";

        public static string JsonCredentialsPath = "C:\\Bob\\GDocToPdf\\Documents\\ServiceAccount_pdf-downloader_credentials.json";
        public static string ExportPath = $"C:\\Bob\\GDocToPdf\\GDriveExport\\";

        public static string LocalStorageVersion = "v1.0";
        public static string LocalStoragePath = $"C:\\Bob\\GDocToPdf\\Documents\\LocalStorage.json";
    }
}
