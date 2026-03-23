using System;
using System.IO;
using System.Threading.Tasks;
using GDriveBackup.Core.Constants;
using Google.Apis.Drive.v3;
using File = Google.Apis.Drive.v3.Data.File;

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    public class GoogleDriveDownloaderAudio : GoogleDriveDownloader
    {
        public GoogleDriveDownloaderAudio(DriveService service)
            : base(service)
        {
        }

        public override Task DownloadFileAsync(string localPath, File file)
        {
            var extension = ResolveExtension(file);
            var mimeType = file?.MimeType ?? MimeTypeConstants.AudioMp4;
            return base.DoDownloadFileAsync(localPath, extension, mimeType, file);
        }

        public override void DownloadAll(DateTime since)
        {
            // Audio files are discovered during folder walk and downloaded per file.
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
                case "audio/mp4":
                    return FileExtensionConstants.M4a;
                case "audio/mpeg":
                    return FileExtensionConstants.Mp3;
                case "audio/wav":
                case "audio/wave":
                    return FileExtensionConstants.Wav;
                default:
                    return ".audio";
            }
        }
    }
}
