using GDriveBackup.Core.Constants;
using GDriveBackup.Core.Extensions;
using Google.Apis.Drive.v3.Data;
using System;

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    /// <summary>
    /// Planner for oversized Google Docs export failures.
    /// This class does not mutate Drive content; it prepares split-plan metadata that
    /// can be used by a later execute phase (Apps Script or Docs API worker).
    /// </summary>
    public class GoogleDriveDownloaderGdocForLargeFiles
    {
        public class LargeGdocSplitPlanItem
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public string MimeType { get; set; }
            public string SourceWebLink { get; set; }
            public string FailureReason { get; set; }
            public string AttemptedExportMimeType { get; set; }
            public string SuggestedSplitMode { get; set; }
            public int SuggestedMaxCharsPerPart { get; set; }
            public bool KeepTemporarySplitDocs { get; set; }
            public string SuggestedOutputFileNamePrefix { get; set; }
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

        public static LargeGdocSplitPlanItem CreateSplitPlanItem(
            File file,
            string failureReason,
            string attemptedExportMimeType,
            string splitMode,
            int maxCharsPerPart,
            bool keepTemporarySplitDocs)
        {
            return new LargeGdocSplitPlanItem
            {
                Name = file?.Name,
                Id = file?.Id,
                MimeType = file?.MimeType,
                SourceWebLink = BuildSourceWebLink(file),
                FailureReason = failureReason,
                AttemptedExportMimeType = attemptedExportMimeType,
                SuggestedSplitMode = splitMode,
                SuggestedMaxCharsPerPart = maxCharsPerPart,
                KeepTemporarySplitDocs = keepTemporarySplitDocs,
                SuggestedOutputFileNamePrefix = file?.Name.ToValidFileName().ReplaceSpaceCharacters(),
                ManualActionHint = "Split this document into smaller parts (e.g. Heading1 boundaries), then rerun backup."
            };
        }
    }
}
