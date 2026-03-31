using GDriveBackup.Core.Extensions;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    /// <summary>
    /// Oversized Google Doc fallback executor.
    ///
    /// We previously tried multiple in-code split strategies (copy+trim ranges, recursive splits,
    /// and stitched PDF output) to work around Drive API PDF export limits. In practice this added
    /// high complexity and still failed frequently due to Docs API invalid range constraints and
    /// export size limits on derived parts. To keep backup behavior predictable, we reverted to a
    /// minimal fallback flow:
    /// 1) create a local manual marker file with the direct link,
    /// 2) include fallback metadata in the unified failed-downloads report.
    /// </summary>
    public class GoogleDriveLargeGdocFallbackProcessor
    {
        public class LargeGdocFallbackCandidate
        {
            public Google.Apis.Drive.v3.Data.File SourceFile { get; set; }
            public string LocalPath { get; set; }
            public string FailureReason { get; set; }
            public string AttemptedExportMimeType { get; set; }
        }

        public class LargeGdocFallbackExecutionItem
        {
            public string SourceFileId { get; set; }
            public string SourceFileName { get; set; }
            public string Status { get; set; }
            public string Details { get; set; }
            public string SourceWebLink { get; set; }
            public string ManualMarkerPath { get; set; }
        }

        public class LargeGdocFallbackExecutionResult
        {
            public int Candidates { get; set; }
            public int Attempted { get; set; }
            public int Failed { get; set; }
            public List<LargeGdocFallbackExecutionItem> Items { get; set; } = new List<LargeGdocFallbackExecutionItem>();
        }

        private readonly IApplicationLogger _logger;

        public GoogleDriveLargeGdocFallbackProcessor(DriveService driveService, GoogleCredential credential, IApplicationLogger logger)
        {
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<LargeGdocFallbackExecutionResult> ExecuteAsync(
            IList<LargeGdocFallbackCandidate> candidates,
            string runId)
        {
            var result = new LargeGdocFallbackExecutionResult
            {
            };

            if (candidates == null || candidates.Count <= 0)
            {
                return await Task.FromResult(result).ConfigureAwait(false);
            }

            var effectiveCandidates = candidates
                .Where(c => GoogleDriveDownloaderGdocForLargeFiles.IsOversizedGdocExportFailure(
                    c.SourceFile,
                    c.AttemptedExportMimeType,
                    false,
                    c.FailureReason))
                .ToList();

            result.Candidates = effectiveCandidates.Count;
            foreach (var candidate in effectiveCandidates)
            {
                var item = new LargeGdocFallbackExecutionItem
                {
                    SourceFileId = candidate.SourceFile?.Id,
                    SourceFileName = candidate.SourceFile?.Name,
                    SourceWebLink = GoogleDriveDownloaderGdocForLargeFiles.BuildSourceWebLink(candidate.SourceFile)
                };
                result.Items.Add(item);
                result.Attempted++;

                try
                {
                    var manualMarkerPath = this.WriteManualMarkerFile(
                        candidate.SourceFile,
                        candidate.LocalPath,
                        item.SourceWebLink,
                        candidate.FailureReason);

                    item.ManualMarkerPath = manualMarkerPath;
                    item.Status = "failed";
                    item.Details = "PDF export limit exceeded. Saved manual marker for manual export fallback.";
                    result.Failed++;

                    this._logger.Warn(
                        $"RunId [{runId}] oversized GDoc fallback prepared: Name [{candidate.SourceFile?.Name}], Id [{candidate.SourceFile?.Id}], Link [{item.SourceWebLink}], ManualMarker [{manualMarkerPath}].");
                }
                catch (Exception ex)
                {
                    item.Status = "failed";
                    item.Details = $"Manual marker creation failed: {ex.Message}";
                    result.Failed++;

                    this._logger.Error(
                        $"RunId [{runId}] oversized GDoc fallback failed for [{candidate.SourceFile?.Name}] (Id [{candidate.SourceFile?.Id}]).",
                        ex);
                }
            }

            return await Task.FromResult(result).ConfigureAwait(false);
        }

        private string WriteManualMarkerFile(
            Google.Apis.Drive.v3.Data.File sourceFile,
            string localPath,
            string sourceWebLink,
            string failureReason)
        {
            var targetDirectory = string.IsNullOrWhiteSpace(localPath)
                ? ApplicationSettings.GetInstance().ExportPath
                : localPath;
            Directory.CreateDirectory(targetDirectory);

            var baseName = (sourceFile?.Name ?? "gdoc").ToValidFileName().ReplaceSpaceCharacters();
            var fileName = $"{baseName}-manual.txt";
            var markerPath = Path.Combine(targetDirectory, fileName);
            if (markerPath.Length > 240)
            {
                var hash = this.ComputeStableHash(sourceFile?.Id ?? baseName).Substring(0, 10);
                markerPath = Path.Combine(targetDirectory, $"{hash}-manual.txt");
            }

            var contents = new StringBuilder();
            contents.AppendLine(sourceWebLink ?? string.Empty);
            contents.AppendLine();
            contents.AppendLine($"Name: {sourceFile?.Name}");
            contents.AppendLine($"FileId: {sourceFile?.Id}");
            contents.AppendLine($"Reason: {failureReason}");
            contents.AppendLine("Action: Download/export manually from Google Docs UI.");

            File.WriteAllText(markerPath, contents.ToString());
            return markerPath;
        }

        private string ComputeStableHash(string value)
        {
            using (var sha1 = SHA1.Create())
            {
                var data = Encoding.UTF8.GetBytes(value ?? string.Empty);
                var hash = sha1.ComputeHash(data);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.AppendFormat("{0:x2}", b);
                }

                return builder.ToString();
            }
        }
    }
}
