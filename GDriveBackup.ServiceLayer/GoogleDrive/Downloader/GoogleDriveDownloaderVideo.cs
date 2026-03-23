using System;
using System.IO;
using System.Threading.Tasks;
using GDriveBackup.Core.Constants;
using Google.Apis.Drive.v3;
using File = Google.Apis.Drive.v3.Data.File;

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    public class GoogleDriveDownloaderVideo : GoogleDriveDownloader
    {
        public GoogleDriveDownloaderVideo(DriveService service)
            : base(service)
        {
        }

        public override Task DownloadFileAsync(string localPath, File file)
        {
            var extension = ResolveExtension(file);
            var mimeType = file?.MimeType ?? MimeTypeConstants.VideoMp4;
            return base.DoDownloadFileAsync(localPath, extension, mimeType, file);
        }

        public override void DownloadAll(DateTime since)
        {
            // Videos are discovered during folder walk and downloaded per file.
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
                case "video/mp4":
                    return FileExtensionConstants.Mp4;
                default:
                    return ".video";
            }
        }
    }
}
