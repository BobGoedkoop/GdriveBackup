using GDriveBackup.Core.Extensions;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
    /// minimal fallback flow (#5):
    /// 1) record direct document links,
    /// 2) capture a native Docs JSON snapshot for backup traceability,
    /// 3) emit a clear report for manual UI export when needed.
    /// </summary>
    public class GoogleDriveLargeGdocAutoSplitter
    {
        public class LargeGdocSplitCandidate
        {
            public Google.Apis.Drive.v3.Data.File SourceFile { get; set; }
            public string LocalPath { get; set; }
            public string FailureReason { get; set; }
            public string AttemptedExportMimeType { get; set; }
        }

        public class LargeGdocSplitExecutionItem
        {
            public string SourceFileId { get; set; }
            public string SourceFileName { get; set; }
            public string Status { get; set; }
            public string Details { get; set; }
            public string SourceWebLink { get; set; }
            public string SnapshotPath { get; set; }
            public string ManualMarkerPath { get; set; }
        }

        public class LargeGdocSplitExecutionResult
        {
            public string RunId { get; set; }
            public DateTime GeneratedUtc { get; set; }
            public bool DryRun { get; set; }
            public int Candidates { get; set; }
            public int Attempted { get; set; }
            public int Succeeded { get; set; }
            public int Failed { get; set; }
            public string ReportPath { get; set; }
            public List<string> ResolvedSourceFileIds { get; set; } = new List<string>();
            public List<LargeGdocSplitExecutionItem> Items { get; set; } = new List<LargeGdocSplitExecutionItem>();
        }

        private readonly GoogleCredential _credential;
        private readonly IApplicationLogger _logger;

        public GoogleDriveLargeGdocAutoSplitter(DriveService driveService, GoogleCredential credential, IApplicationLogger logger)
        {
            this._credential = credential ?? throw new ArgumentNullException(nameof(credential));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<LargeGdocSplitExecutionResult> ExecuteAsync(
            IList<LargeGdocSplitCandidate> candidates,
            string runId)
        {
            var result = new LargeGdocSplitExecutionResult
            {
                RunId = runId,
                GeneratedUtc = DateTime.UtcNow,
                DryRun = false
            };

            if (candidates == null || candidates.Count <= 0)
            {
                return await this.WriteExecutionReportAsync(result).ConfigureAwait(false);
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
                var item = new LargeGdocSplitExecutionItem
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
                    var snapshotPath = await this.WriteDocSnapshotAsync(
                            runId,
                            candidate.SourceFile,
                            candidate.LocalPath)
                        .ConfigureAwait(false);

                    item.ManualMarkerPath = manualMarkerPath;
                    item.SnapshotPath = snapshotPath;
                    item.Status = "failed";
                    item.Details = "PDF export limit exceeded. Saved manual marker + link + Docs JSON snapshot for manual export fallback.";
                    result.Failed++;

                    this._logger.Warn(
                        $"RunId [{runId}] oversized GDoc fallback prepared: Name [{candidate.SourceFile?.Name}], Id [{candidate.SourceFile?.Id}], Link [{item.SourceWebLink}], ManualMarker [{manualMarkerPath}], Snapshot [{snapshotPath}].");
                }
                catch (Exception ex)
                {
                    item.Status = "failed";
                    item.Details = $"Fallback snapshot failed: {ex.Message}";
                    result.Failed++;

                    this._logger.Error(
                        $"RunId [{runId}] oversized GDoc fallback failed for [{candidate.SourceFile?.Name}] (Id [{candidate.SourceFile?.Id}]).",
                        ex);
                }
            }

            return await this.WriteExecutionReportAsync(result).ConfigureAwait(false);
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

        private async Task<string> WriteDocSnapshotAsync(
            string runId,
            Google.Apis.Drive.v3.Data.File sourceFile,
            string localPath)
        {
            if (sourceFile == null || string.IsNullOrWhiteSpace(sourceFile.Id))
            {
                throw new ArgumentException("Fallback snapshot requires a valid source file id.");
            }

            var documentJson = await this.GetDocumentJsonAsync(sourceFile.Id).ConfigureAwait(false);
            var snapshotRoot = Path.Combine(
                ApplicationSettings.GetInstance().ExportPath,
                "_reports",
                "large-gdoc-fallback-snapshots",
                runId);
            Directory.CreateDirectory(snapshotRoot);

            var safeName = (sourceFile.Name ?? "gdoc").ToValidFileName().ReplaceSpaceCharacters();
            var hash = this.ComputeStableHash(sourceFile.Id).Substring(0, 10);
            var fileName = $"{safeName}_{hash}.gdoc.json";
            var path = Path.Combine(snapshotRoot, fileName);

            if (path.Length > 240)
            {
                fileName = $"{hash}.gdoc.json";
                path = Path.Combine(snapshotRoot, fileName);
            }

            var payload = new JObject
            {
                ["runId"] = runId,
                ["generatedUtc"] = DateTime.UtcNow,
                ["sourceFileId"] = sourceFile.Id,
                ["sourceFileName"] = sourceFile.Name,
                ["sourceMimeType"] = sourceFile.MimeType,
                ["sourceLocalPath"] = localPath ?? string.Empty,
                ["sourceWebLink"] = GoogleDriveDownloaderGdocForLargeFiles.BuildSourceWebLink(sourceFile),
                ["note"] = "API PDF export exceeded size limit; this is a native Docs JSON snapshot fallback.",
                ["document"] = documentJson
            };

            File.WriteAllText(path, payload.ToString(Formatting.Indented));
            return path;
        }

        private async Task<JObject> GetDocumentJsonAsync(string docId)
        {
            using (var client = await this.CreateAuthorizedHttpClientAsync().ConfigureAwait(false))
            {
                var response = await client.GetAsync($"https://docs.googleapis.com/v1/documents/{docId}").ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var body = response.Content == null ? string.Empty : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new HttpRequestException(
                        $"GET documents/{docId} failed with status [{(int)response.StatusCode} {response.StatusCode}] body [{body}]");
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JObject.Parse(json);
            }
        }

        private async Task<HttpClient> CreateAuthorizedHttpClientAsync()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(ApplicationSettings.GetInstance().DriveApiRequestTimeoutSeconds)
            };

            var token = await this._credential.UnderlyingCredential
                .GetAccessTokenForRequestAsync()
                .ConfigureAwait(false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private async Task<LargeGdocSplitExecutionResult> WriteExecutionReportAsync(LargeGdocSplitExecutionResult result)
        {
            var reportDir = Path.Combine(ApplicationSettings.GetInstance().ExportPath, "_reports");
            Directory.CreateDirectory(reportDir);
            var reportPath = Path.Combine(reportDir, $"large-gdoc-fallback-execution-{result.RunId}.json");
            File.WriteAllText(reportPath, JsonConvert.SerializeObject(result, Formatting.Indented));
            result.ReportPath = reportPath;
            return await Task.FromResult(result).ConfigureAwait(false);
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
