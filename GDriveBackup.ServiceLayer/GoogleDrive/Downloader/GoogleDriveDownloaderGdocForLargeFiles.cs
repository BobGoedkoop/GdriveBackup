using GDriveBackup.Core.Constants;
using Google.Apis.Drive.v3.Data;
using System;

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    /// <summary>
    /// Oversized Google Doc detection + fallback metadata.
    /// The fallback is "link + snapshot + clear report", not in-place splitting.
    /// </summary>
    public class GoogleDriveDownloaderGdocForLargeFiles
    {
        public static bool IsOversizedGdocExportFailure(
            File file,
            string attemptedExportMimeType,
            bool isRetryable,
            string failureReason)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.Id))
            {
                return false;
            }

            if (isRetryable)
            {
                return false;
            }

            if (!string.Equals(file.MimeType, MimeTypeConstants.Gdoc, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(attemptedExportMimeType, MimeTypeConstants.ApplicationPdf, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var reason = failureReason ?? string.Empty;
            return reason.IndexOf("too large", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string BuildSourceWebLink(File file)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.Id))
            {
                return string.Empty;
            }

            return $"https://docs.google.com/document/d/{file.Id}/edit";
        }
    }
}
