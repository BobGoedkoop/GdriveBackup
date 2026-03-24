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
        public class LargeGdocFallbackPlanItem
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public string MimeType { get; set; }
            public string SourceWebLink { get; set; }
            public string FailureReason { get; set; }
            public string AttemptedExportMimeType { get; set; }
            public string ManualActionHint { get; set; }
        }

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

        public static LargeGdocFallbackPlanItem CreateFallbackPlanItem(
            File file,
            string failureReason,
            string attemptedExportMimeType)
        {
            return new LargeGdocFallbackPlanItem
            {
                Name = file?.Name,
                Id = file?.Id,
                MimeType = file?.MimeType,
                SourceWebLink = BuildSourceWebLink(file),
                FailureReason = failureReason,
                AttemptedExportMimeType = attemptedExportMimeType,
                ManualActionHint = "Export manually from Google Docs UI when Drive API PDF export limit is exceeded."
            };
        }
    }
}
