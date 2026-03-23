using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GDriveBackup.BusinessLayer.Domain.Run;
using GDriveBackup.Core.Constants;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;
using GDriveBackup.ServiceLayer.GoogleDrive.Authenticate;
using GDriveBackup.ServiceLayer.GoogleDrive.Downloader;
using GDriveBackup.ServiceLayer.GoogleDrive.Files;
using GDriveBackup.ServiceLayer.GoogleDrive.FolderWalker;
using GDriveBackup.ServiceLayer.GoogleDrive.Service;
using Google.Apis.Drive.v3;
using Newtonsoft.Json;

// ReSharper disable IdentifierTypo

namespace GDriveBackup.BusinessLayer.Domain.Backup
{
    public class FailedDownload
    {
        public string LocalPath { get; set; } = string.Empty;
        public string LocalExt { get; set; } = string.Empty;
        public string LocelMimeType { get; set; } = string.Empty;
        public Google.Apis.Drive.v3.Data.File GDriveFile { get; set; } = null;
        public bool IsRetryable { get; set; } = true;
        public string FailureReason { get; set; } = string.Empty;
    }


    public class BackupDomain
    {
        private class FailedExportReport
        {
            public string RunId { get; set; }
            public DateTime StartedUtc { get; set; }
            public DateTime GeneratedUtc { get; set; }
            public int Count { get; set; }
            public List<FailedExportReportItem> Items { get; set; }
        }

        private class FailedExportReportItem
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public string MimeType { get; set; }
            public string FailureReason { get; set; }
            public string LocalPath { get; set; }
            public string AttemptedExportMimeType { get; set; }
            public string WebLink { get; set; }
        }

        private class LargeGdocSplitPlanReport
        {
            public string RunId { get; set; }
            public DateTime StartedUtc { get; set; }
            public DateTime GeneratedUtc { get; set; }
            public string SplitMode { get; set; }
            public int MaxCharsPerPart { get; set; }
            public bool KeepTemporarySplitDocs { get; set; }
            public int Count { get; set; }
            public List<GoogleDriveDownloaderGdocForLargeFiles.LargeGdocSplitPlanItem> Items { get; set; }
        }

        private const int DefaultMaxConcurrentDownloads = 6;

        private readonly DateTime _lastRunDate;
        private readonly IApplicationLogger _logger;
        private readonly int _maxConcurrentDownloads;

        private string _runId;
        private DateTime _startRunDate;
        private ConcurrentBag<FailedDownload> _failedDownloadList;
        private List<Task> _folderDownloadTasks;
        private SemaphoreSlim _downloadSemaphore;
        private long _foldersVisited;
        private long _filesDiscovered;
        private long _downloadAttempts;
        private long _initialFailuresDetected;
        private long _retryAttempts;
        private long _retryCompletedCalls;
        private long _nonRetryableFailures;
        private string _failedExportsReportPath;
        private string _largeGdocSplitPlanPath;
        private long _largeGdocSplitCandidates;


        #region Private section

        private void DoDownloadFailedHandler(
            string localPath,
            string localExt,
            string localMimeType,
            Google.Apis.Drive.v3.Data.File file,
            bool isRetryable,
            string failureReason)
        {
            Interlocked.Increment(ref this._initialFailuresDetected);

            if (!isRetryable)
            {
                Interlocked.Increment(ref this._nonRetryableFailures);
            }

            if (isRetryable)
            {
                this._logger.Warn(
                    $"RunId [{this._runId}] download failed and queued for retry: Name [{file?.Name}], Id [{file?.Id}], MimeType [{file?.MimeType}], LocalPath [{localPath}], Reason [{failureReason}].",
                    new Dictionary<string, object>
                    {
                        ["RunId"] = this._runId,
                        ["FileId"] = file?.Id ?? string.Empty,
                        ["MimeType"] = file?.MimeType ?? string.Empty,
                        ["LocalPath"] = localPath ?? string.Empty
                    });
            }
            else
            {
                this._logger.Warn(
                    $"RunId [{this._runId}] non-retryable download failure: Name [{file?.Name}], Id [{file?.Id}], MimeType [{file?.MimeType}], Reason [{failureReason}].",
                    new Dictionary<string, object>
                    {
                        ["RunId"] = this._runId,
                        ["FileId"] = file?.Id ?? string.Empty,
                        ["MimeType"] = file?.MimeType ?? string.Empty
                    });
            }

            this._failedDownloadList.Add(new FailedDownload()
            {
                LocalPath = localPath,
                LocalExt = localExt,
                LocelMimeType = localMimeType,
                GDriveFile = file,
                IsRetryable = isRetryable,
                FailureReason = failureReason
            });
        }

        private async Task DownloadFilesInFolderAsync(DriveService service, WalkerCurrentFolder currentFolder)
        {
            try
            {
                var gDriveFolder = new GoogleDriveFolder(service);
                var fileList = gDriveFolder.GetFilesInFolder(currentFolder.GDriveFile.Id, this._lastRunDate);
                Interlocked.Add(ref this._filesDiscovered, fileList.Files.Count);
                this._logger.Debug(
                    $"RunId [{this._runId}] folder [{currentFolder.GDriveFile.Name}] queued [{fileList.Files.Count}] files.");
                var downloadTasks = new List<Task>();

                foreach (var file in fileList.Files)
                {
                    var downloader = GoogleDriveDownloaderFactory
                        .GetInstance()
                        .GetDownloader(service, file);

                    downloader.OnFailed = DoDownloadFailedHandler;
                    downloadTasks.Add(
                        this.DownloadFileWithThrottleAsync(downloader, currentFolder.LocalFullPath, file)
                    );
                }

                await Task.WhenAll(downloadTasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.Error(
                    $"RunId [{this._runId}] failed while downloading folder [{currentFolder.GDriveFile.Name}] ({currentFolder.GDriveFile.Id}).",
                    ex);
                this._logger.Error(
                    $"RunId [{this._runId}] folder download task failed.",
                    new Dictionary<string, object>
                    {
                        ["RunId"] = this._runId,
                        ["FolderId"] = currentFolder?.GDriveFile?.Id ?? string.Empty,
                        ["FolderName"] = currentFolder?.GDriveFile?.Name ?? string.Empty
                    });
                //throw;  todo Add to failed download handler if not already done.
            }
        }

        private async Task DownloadFileWithThrottleAsync(
            GoogleDriveDownloader downloader,
            string localPath,
            Google.Apis.Drive.v3.Data.File file)
        {
            Interlocked.Increment(ref this._downloadAttempts);
            await this._downloadSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await downloader.DownloadFileAsync(localPath, file).ConfigureAwait(false);
            }
            finally
            {
                this._downloadSemaphore.Release();
            }
        }

        private void DoFolderHandler(DriveService service, WalkerCurrentFolder currentFolder)
        {
            Interlocked.Increment(ref this._foldersVisited);
            this._logger.Debug(
                $"RunId [{this._runId}] walking folder [{currentFolder.GDriveFile.Name}] => [{currentFolder.LocalFullPath}].");

            try
            {
                // This will create the directory if it does not exist yet.
                Directory.CreateDirectory(currentFolder.LocalFullPath);
            }
            catch (PathTooLongException ex)
            {
                this._logger.Error($"Path too long [{currentFolder.LocalFullPath}].", ex);
                throw;
            }

            this._folderDownloadTasks.Add(
                this.DownloadFilesInFolderAsync(service, currentFolder)
            );
        }

        private void DoFinishedHandler(DriveService service)
        {
            // Intentionally no-op: completion is handled in StartAsync after awaited folder tasks.
        }

        private void UpdateLastRunDate()
        {
            var runDateDomain = new RunDateDomain();
            runDateDomain.LastRunDate = this._startRunDate;
            this._logger.Info(
                $"RunId [{this._runId}] checkpoint updated: LastRunDate [{this._startRunDate:o}].");
        }

        private async Task RetryFailedDownloadsAsync(DriveService service)
        {
            var failedDownloads = this._failedDownloadList.ToList();
            var retryableDownloads = failedDownloads
                .Where(failedDownload => failedDownload.IsRetryable)
                .ToList();

            foreach (var failedDownload in retryableDownloads)
            {
                try
                {
                    Interlocked.Increment(ref this._retryAttempts);
                    Interlocked.Increment(ref this._downloadAttempts);
                    this._logger.Warn(
                        $"RunId [{this._runId}] retrying download: Name [{failedDownload.GDriveFile.Name}], Id [{failedDownload.GDriveFile.Id}], LocalPath [{failedDownload.LocalPath}].",
                        new Dictionary<string, object>
                        {
                            ["RunId"] = this._runId,
                            ["RetryAttempt"] = this._retryAttempts,
                            ["FileId"] = failedDownload.GDriveFile.Id ?? string.Empty,
                            ["MimeType"] = failedDownload.GDriveFile.MimeType ?? string.Empty,
                            ["LocalPath"] = failedDownload.LocalPath ?? string.Empty
                        });

                    await GoogleDriveDownloaderFactory.GetInstance()
                        .GetDownloader(service, failedDownload.GDriveFile)
                        .DownloadFileAsync(failedDownload.LocalPath, failedDownload.GDriveFile)
                        .ConfigureAwait(false);
                    Interlocked.Increment(ref this._retryCompletedCalls);
                }
                catch (Exception ex)
                {
                    this._logger.Error(
                        $"The retry of the failed download ([{failedDownload.GDriveFile.Name}] to [{failedDownload.LocalPath}]) failed.",
                        ex);
                }
            }
        }

        private static string BuildDriveWebLink(Google.Apis.Drive.v3.Data.File file)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.Id))
            {
                return string.Empty;
            }

            // Use Google-doc-specific links when possible; they open directly in the editor.
            if (file.MimeType == MimeTypeConstants.Gdoc)
            {
                return $"https://docs.google.com/document/d/{file.Id}/edit";
            }

            if (file.MimeType == MimeTypeConstants.Gsheet)
            {
                return $"https://docs.google.com/spreadsheets/d/{file.Id}/edit";
            }

            // Generic Drive fallback for other file types.
            return $"https://drive.google.com/open?id={file.Id}";
        }

        private string WriteFailedExportsReportAndLogSummary()
        {
            var nonRetryableFailures = this._failedDownloadList
                .Where(f => !f.IsRetryable && f.GDriveFile != null && !string.IsNullOrWhiteSpace(f.GDriveFile.Id))
                .GroupBy(f => f.GDriveFile.Id)
                .Select(group => group.Last())
                .ToList();

            if (!nonRetryableFailures.Any())
            {
                this._logger.Info($"RunId [{this._runId}] no non-retryable failed exports to report.");
                return string.Empty;
            }

            var reportItems = nonRetryableFailures
                .Select(f => new FailedExportReportItem
                {
                    Name = f.GDriveFile.Name,
                    Id = f.GDriveFile.Id,
                    MimeType = f.GDriveFile.MimeType,
                    FailureReason = f.FailureReason,
                    LocalPath = f.LocalPath,
                    AttemptedExportMimeType = f.LocelMimeType,
                    WebLink = BuildDriveWebLink(f.GDriveFile)
                })
                .OrderBy(item => item.Name)
                .ToList();

            var report = new FailedExportReport
            {
                RunId = this._runId,
                StartedUtc = this._startRunDate,
                GeneratedUtc = DateTime.UtcNow,
                Count = reportItems.Count,
                Items = reportItems
            };

            var reportDir = Path.Combine(ApplicationSettings.GetInstance().ExportPath, "_reports");
            Directory.CreateDirectory(reportDir);
            var reportPath = Path.Combine(reportDir, $"failed-exports-{this._runId}.json");
            File.WriteAllText(reportPath, JsonConvert.SerializeObject(report, Formatting.Indented));

            this._logger.Warn(
                $"RunId [{this._runId}] non-retryable failed exports report created: [{reportPath}] (Count [{reportItems.Count}]).");

            foreach (var item in reportItems)
            {
                this._logger.Warn(
                    $"RunId [{this._runId}] manual-download candidate: Name [{item.Name}], Id [{item.Id}], Reason [{item.FailureReason}], Link [{item.WebLink}].");
            }

            return reportPath;
        }

        private string WriteLargeGdocSplitPlanReportAndLogSummary()
        {
            var settings = ApplicationSettings.GetInstance();
            if (!settings.EnableLargeGdocAutoSplit)
            {
                this._logger.Info($"RunId [{this._runId}] large GDoc auto-split planner disabled.");
                return string.Empty;
            }

            var candidates = this._failedDownloadList
                .Where(f => GoogleDriveDownloaderGdocForLargeFiles.IsOversizedGdocExportFailure(
                    f.GDriveFile,
                    f.LocelMimeType,
                    f.IsRetryable,
                    f.FailureReason))
                .GroupBy(f => f.GDriveFile.Id)
                .Select(group => group.Last())
                .ToList();

            Interlocked.Exchange(ref this._largeGdocSplitCandidates, candidates.Count);

            if (!candidates.Any())
            {
                this._logger.Info($"RunId [{this._runId}] no oversized Google Docs found for split-plan.");
                return string.Empty;
            }

            var splitPlanItems = candidates
                .Select(f => GoogleDriveDownloaderGdocForLargeFiles.CreateSplitPlanItem(
                    f.GDriveFile,
                    f.FailureReason,
                    f.LocelMimeType,
                    settings.LargeGdocSplitMode,
                    settings.LargeGdocMaxCharsPerPart,
                    settings.KeepTemporarySplitDocs))
                .OrderBy(item => item.Name)
                .ToList();

            var report = new LargeGdocSplitPlanReport
            {
                RunId = this._runId,
                StartedUtc = this._startRunDate,
                GeneratedUtc = DateTime.UtcNow,
                SplitMode = settings.LargeGdocSplitMode,
                MaxCharsPerPart = settings.LargeGdocMaxCharsPerPart,
                KeepTemporarySplitDocs = settings.KeepTemporarySplitDocs,
                Count = splitPlanItems.Count,
                Items = splitPlanItems
            };

            var reportDir = Path.Combine(settings.ExportPath, "_reports");
            Directory.CreateDirectory(reportDir);
            var reportPath = Path.Combine(reportDir, $"large-gdoc-split-plan-{this._runId}.json");
            File.WriteAllText(reportPath, JsonConvert.SerializeObject(report, Formatting.Indented));

            this._logger.Warn(
                $"RunId [{this._runId}] large GDoc split-plan report created: [{reportPath}] (Count [{splitPlanItems.Count}], Mode [{settings.LargeGdocSplitMode}], MaxCharsPerPart [{settings.LargeGdocMaxCharsPerPart}]).");

            foreach (var item in splitPlanItems)
            {
                this._logger.Warn(
                    $"RunId [{this._runId}] large GDoc split-plan candidate: Name [{item.Name}], Id [{item.Id}], Link [{item.SourceWebLink}], Reason [{item.FailureReason}].");
            }

            return reportPath;
        }

        #endregion


        public BackupDomain(DateTime lastRunDate)
        {
            this._lastRunDate = lastRunDate;
            this._logger = ApplicationLogger.GetInstance();

            // Keep one place where we resolve and sanitize tuning knobs from config.
            var configuredConcurrency = ApplicationSettings.GetInstance().MaxConcurrentDownloads;
            this._maxConcurrentDownloads = configuredConcurrency > 0
                ? configuredConcurrency
                : DefaultMaxConcurrentDownloads;
        }

        public void Start()
        {
            this.StartAsync().GetAwaiter().GetResult();
        }

        public async Task StartAsync()
        {
            this._runId = Guid.NewGuid().ToString("N");
            this._logger.SetContext("RunId", this._runId);
            this._startRunDate = DateTime.UtcNow;
            this._failedDownloadList = new ConcurrentBag<FailedDownload>();
            this._folderDownloadTasks = new List<Task>();
            this._foldersVisited = 0;
            this._filesDiscovered = 0;
            this._downloadAttempts = 0;
            this._initialFailuresDetected = 0;
            this._retryAttempts = 0;
            this._retryCompletedCalls = 0;
            this._nonRetryableFailures = 0;
            this._failedExportsReportPath = string.Empty;
            this._largeGdocSplitPlanPath = string.Empty;
            this._largeGdocSplitCandidates = 0;
            // Global download throttle to avoid overwhelming Drive API and local I/O.
            this._downloadSemaphore = new SemaphoreSlim(this._maxConcurrentDownloads, this._maxConcurrentDownloads);
            var runCompleted = false;

            try
            {
                var mode = this._lastRunDate == DateTime.MinValue ? "all" : "changes";
                this._logger.Info(
                    $"Backup run started. RunId [{this._runId}], Mode [{mode}], LastRunDate [{this._lastRunDate:o}], ExportPath [{ApplicationSettings.GetInstance().ExportPath}], MaxConcurrentDownloads [{this._maxConcurrentDownloads}].");

                var gAuth = new GoogleDriveAuthenticate();
                var credential = gAuth.Authenticate();

                var gService = new GoogleDriveService();
                var service = gService.GetService(credential);


                var walker = new GoogleDriveFolderWalker(service)
                {
                    OnFolder = DoFolderHandler,
                    OnFinished = DoFinishedHandler
                };

                walker.Walk();
                await Task.WhenAll(this._folderDownloadTasks).ConfigureAwait(false);

                await this.RetryFailedDownloadsAsync(service).ConfigureAwait(false);
                this.UpdateLastRunDate();
                runCompleted = true;
            }
            catch (Exception ex)
            {
                this._logger.Error(
                    $"Backup run aborted due to an unhandled exception. RunId [{this._runId}].",
                    ex);
            }
            finally
            {
                // Best-effort report generation must run even when backup aborts early.
                // This ensures failed-export artifacts are still available for manual follow-up.
                try
                {
                    if (string.IsNullOrWhiteSpace(this._failedExportsReportPath))
                    {
                        this._failedExportsReportPath = this.WriteFailedExportsReportAndLogSummary();
                    }

                    if (string.IsNullOrWhiteSpace(this._largeGdocSplitPlanPath))
                    {
                        this._largeGdocSplitPlanPath = this.WriteLargeGdocSplitPlanReportAndLogSummary();
                    }
                }
                catch (Exception ex)
                {
                    this._logger.Error(
                        $"RunId [{this._runId}] failed while generating end-of-run reports.",
                        ex);
                }

                var duration = DateTime.UtcNow - this._startRunDate;
                var runStatus = runCompleted ? "completed" : "aborted";
                this._logger.Info(
                    $"Backup run {runStatus}. RunId [{this._runId}], Duration [{duration:dd\\.hh\\:mm\\:ss\\:fff}], FoldersVisited [{this._foldersVisited}], FilesDiscovered [{this._filesDiscovered}], DownloadCalls [{this._downloadAttempts}], InitialFailures [{this._initialFailuresDetected}], RetryAttempts [{this._retryAttempts}], RetryCallsCompleted [{this._retryCompletedCalls}], NonRetryableFailures [{this._nonRetryableFailures}], FailedExportsReport [{this._failedExportsReportPath}], LargeGdocSplitCandidates [{this._largeGdocSplitCandidates}], LargeGdocSplitPlanReport [{this._largeGdocSplitPlanPath}].");

                this._downloadSemaphore?.Dispose();
                this._logger.ClearContext("RunId");
            }
        }
    }
}
