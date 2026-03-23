using System;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using File = Google.Apis.Drive.v3.Data.File;

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    public class GoogleDriveDownloaderImage : GoogleDriveDownloader
    {
        public GoogleDriveDownloaderImage(DriveService service)
            : base(service)
        {
        }

        public override Task DownloadFileAsync(string localPath, File file)
        {
            var extension = ResolveExtension(file);
            var mimeType = file?.MimeType ?? "application/octet-stream";
            return base.DoDownloadFileAsync(localPath, extension, mimeType, file);
        }

        public override void DownloadAll(DateTime since)
        {
            // Image files are discovered while walking folders, not via one global mime-type query.
            // Keep implementation explicit to avoid accidental "download all images" scans here.
        }

        private static string ResolveExtension(File file)
        {
            var extensionFromName = Path.GetExtension(file?.Name ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(extensionFromName))
            {
                return extensionFromName.StartsWith(".", StringComparison.Ordinal)
                    ? extensionFromName
                    : "." + extensionFromName;
            }

            var mimeType = (file?.MimeType ?? string.Empty).ToLowerInvariant();
            switch (mimeType)
            {
                case "image/jpeg":
                case "image/jpg":
                    return ".jpg";
                case "image/png":
                    return ".png";
                case "image/gif":
                    return ".gif";
                case "image/webp":
                    return ".webp";
                case "image/bmp":
                    return ".bmp";
                case "image/tiff":
                    return ".tif";
                case "image/heic":
                    return ".heic";
                default:
                    return ".img";
            }
        }
    }
}
