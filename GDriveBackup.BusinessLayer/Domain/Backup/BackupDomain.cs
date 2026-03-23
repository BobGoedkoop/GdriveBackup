using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GDriveBackup.BusinessLayer.Domain.Run;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;
using GDriveBackup.ServiceLayer.GoogleDrive.Authenticate;
using GDriveBackup.ServiceLayer.GoogleDrive.Downloader;
using GDriveBackup.ServiceLayer.GoogleDrive.Files;
using GDriveBackup.ServiceLayer.GoogleDrive.FolderWalker;
using GDriveBackup.ServiceLayer.GoogleDrive.Service;
using Google.Apis.Drive.v3;

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
        public bool ResolvedByAutoSplit { get; set; } = false;
    }


    public class BackupDomain
    {
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
        private string _largeGdocSplitExecutionPath;
        private long _largeGdocSplitCandidates;
        private long _largeGdocAutoSplitAttempted;
        private long _largeGdocAutoSplitSucceeded;
        private long _largeGdocAutoSplitFailed;
        private bool _largeGdocAutoSplitDryRun;
        private readonly BackupConsoleHeartbeat _consoleHeartbeat;
        private readonly BackupRunReportService _reportService;
        private readonly BackupLargeGdocAutoSplitService _autoSplitService;


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

        #endregion


        public BackupDomain(DateTime lastRunDate, string autoSplitFileId = "")
        {
            this._lastRunDate = lastRunDate;
            var largeGdocAutoSplitFileId = (autoSplitFileId ?? string.Empty).Trim();
            this._logger = ApplicationLogger.GetInstance();
            this._consoleHeartbeat = new BackupConsoleHeartbeat(this._logger);
            this._reportService = new BackupRunReportService(this._logger);
            this._autoSplitService = new BackupLargeGdocAutoSplitService(this._logger, largeGdocAutoSplitFileId);

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

        public void StartSplitOnly(string fileId)
        {
            this.StartSplitOnlyAsync(fileId).GetAwaiter().GetResult();
        }

        public async Task StartSplitOnlyAsync(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))
            {
                throw new ArgumentException("Split-only mode requires a non-empty Google Drive file id.", nameof(fileId));
            }

            var runId = Guid.NewGuid().ToString("N");
            this._logger.SetContext("RunId", runId);
            var startUtc = DateTime.UtcNow;

            try
            {
                this._logger.Info(
                    $"Split-only run started. RunId [{runId}], FileId [{fileId}], ExportPath [{ApplicationSettings.GetInstance().ExportPath}].");

                var gAuth = new GoogleDriveAuthenticate();
                var credential = gAuth.Authenticate();

                var gService = new GoogleDriveService();
                var service = gService.GetService(credential);

                var result = await this._autoSplitService
                    .ExecuteSplitOnlyAsync(service, credential, runId, fileId)
                    .ConfigureAwait(false);
                if (result == null)
                {
                    return;
                }

                var duration = DateTime.UtcNow - startUtc;
                this._logger.Info(
                    $"Split-only run completed. RunId [{runId}], Duration [{duration:dd\\.hh\\:mm\\:ss\\:fff}], Attempted [{result.Attempted}], Succeeded [{result.Succeeded}], Failed [{result.Failed}], DryRun [{result.DryRun}], Report [{result.ReportPath}].");
            }
            catch (Exception ex)
            {
                this._logger.Error(
                    $"Split-only run aborted due to an unhandled exception. RunId [{runId}], FileId [{fileId}].",
                    ex);
            }
            finally
            {
                this._logger.ClearContext("RunId");
            }
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
            this._largeGdocSplitExecutionPath = string.Empty;
            this._largeGdocSplitCandidates = 0;
            this._largeGdocAutoSplitAttempted = 0;
            this._largeGdocAutoSplitSucceeded = 0;
            this._largeGdocAutoSplitFailed = 0;
            this._largeGdocAutoSplitDryRun = false;
            // Global download throttle to avoid overwhelming Drive API and local I/O.
            this._downloadSemaphore = new SemaphoreSlim(this._maxConcurrentDownloads, this._maxConcurrentDownloads);
            var runCompleted = false;

            try
            {
                var mode = this._lastRunDate == DateTime.MinValue ? "all" : "changes";
                this._logger.Info(
                    $"Backup run started. RunId [{this._runId}], Mode [{mode}], LastRunDate [{this._lastRunDate:o}], ExportPath [{ApplicationSettings.GetInstance().ExportPath}], MaxConcurrentDownloads [{this._maxConcurrentDownloads}].");
                // Keep orchestration in the domain and heartbeat internals in a dedicated class.
                this._consoleHeartbeat.Configure(
                    this._runId,
                    this._startRunDate,
                    () => Interlocked.Read(ref this._foldersVisited),
                    () => Interlocked.Read(ref this._filesDiscovered),
                    () => Interlocked.Read(ref this._downloadAttempts),
                    () => Interlocked.Read(ref this._initialFailuresDetected));
                this._consoleHeartbeat.Start();

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
                var autoSplitResult = await this._autoSplitService
                    .ExecuteForFailedDownloadsAsync(service, credential, this._failedDownloadList.ToList(), this._runId)
                    .ConfigureAwait(false);
                if (autoSplitResult != null)
                {
                    this._largeGdocSplitExecutionPath = autoSplitResult.ReportPath ?? string.Empty;
                    this._largeGdocAutoSplitAttempted = autoSplitResult.Attempted;
                    this._largeGdocAutoSplitSucceeded = autoSplitResult.Succeeded;
                    this._largeGdocAutoSplitFailed = autoSplitResult.Failed;
                    this._largeGdocAutoSplitDryRun = autoSplitResult.DryRun;
                }
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
                        this._failedExportsReportPath = this._reportService.WriteFailedExportsReportAndLogSummary(
                            this._failedDownloadList.ToList(),
                            this._runId,
                            this._startRunDate);
                    }

                    if (string.IsNullOrWhiteSpace(this._largeGdocSplitPlanPath))
                    {
                        this._largeGdocSplitPlanPath = this._reportService.WriteLargeGdocSplitPlanReportAndLogSummary(
                            this._failedDownloadList.ToList(),
                            this._runId,
                            this._startRunDate,
                            out var splitCandidatesCount);
                        Interlocked.Exchange(ref this._largeGdocSplitCandidates, splitCandidatesCount);
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
                    $"Backup run {runStatus}. RunId [{this._runId}], Duration [{duration:dd\\.hh\\:mm\\:ss\\:fff}], FoldersVisited [{this._foldersVisited}], FilesDiscovered [{this._filesDiscovered}], DownloadCalls [{this._downloadAttempts}], InitialFailures [{this._initialFailuresDetected}], RetryAttempts [{this._retryAttempts}], RetryCallsCompleted [{this._retryCompletedCalls}], NonRetryableFailures [{this._nonRetryableFailures}], FailedExportsReport [{this._failedExportsReportPath}], LargeGdocSplitCandidates [{this._largeGdocSplitCandidates}], LargeGdocSplitPlanReport [{this._largeGdocSplitPlanPath}], LargeGdocSplitExecutionReport [{this._largeGdocSplitExecutionPath}], LargeGdocAutoSplitAttempted [{this._largeGdocAutoSplitAttempted}], LargeGdocAutoSplitSucceeded [{this._largeGdocAutoSplitSucceeded}], LargeGdocAutoSplitFailed [{this._largeGdocAutoSplitFailed}], LargeGdocAutoSplitDryRun [{this._largeGdocAutoSplitDryRun}].");

                this._downloadSemaphore?.Dispose();
                this._consoleHeartbeat.Stop();
                this._logger.ClearContext("RunId");
            }
        }
    }
}
