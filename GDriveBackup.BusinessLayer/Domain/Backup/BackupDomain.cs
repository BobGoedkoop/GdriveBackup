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
        public string LocalMimeType { get; set; } = string.Empty;
        public Google.Apis.Drive.v3.Data.File GDriveFile { get; set; } = null;
        public bool IsRetryable { get; set; } = true;
        public string FailureReason { get; set; } = string.Empty;
        public string ManualMarkerPath { get; set; } = string.Empty;
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
        private long _nonRetryableFailures;
        private string _failedDownloadsReportPath;
        private long _largeGdocFallbackAttempted;
        private long _largeGdocFallbackFailed;
        private long _largeGdocFallbackUnresolvedCount;
        private readonly BackupConsoleHeartbeat _consoleHeartbeat;
        private readonly BackupRunReportService _reportService;
        private readonly BackupLargeGdocFallbackService _largeGdocFallbackService;


        #region Private section

        private void DoDownloadFailedHandler(
            string localPath,
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
                LocalMimeType = localMimeType,
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

            if (retryableDownloads.Count <= 0)
            {
                return;
            }

            // Retry in parallel under the same global download throttle used by initial downloads.
            var retryTasks = retryableDownloads
                .Select(failedDownload => this.RetryFailedDownloadWithThrottleAsync(service, failedDownload))
                .ToList();

            await Task.WhenAll(retryTasks).ConfigureAwait(false);
        }

        private async Task RetryFailedDownloadWithThrottleAsync(DriveService service, FailedDownload failedDownload)
        {
            await this._downloadSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                Interlocked.Increment(ref this._downloadAttempts);
                this._logger.Warn(
                    $"RunId [{this._runId}] retrying download: Name [{failedDownload.GDriveFile.Name}], Id [{failedDownload.GDriveFile.Id}], LocalPath [{failedDownload.LocalPath}].",
                    new Dictionary<string, object>
                    {
                        ["RunId"] = this._runId,
                        ["FileId"] = failedDownload.GDriveFile.Id ?? string.Empty,
                        ["MimeType"] = failedDownload.GDriveFile.MimeType ?? string.Empty,
                        ["LocalPath"] = failedDownload.LocalPath ?? string.Empty
                    });

                var downloader = GoogleDriveDownloaderFactory.GetInstance()
                    .GetDownloader(service, failedDownload.GDriveFile);
                downloader.OnFailed = DoDownloadFailedHandler;

                await downloader
                    .DownloadFileAsync(failedDownload.LocalPath, failedDownload.GDriveFile)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.Error(
                    $"The retry of the failed download ([{failedDownload.GDriveFile.Name}] to [{failedDownload.LocalPath}]) failed.",
                    ex);
            }
            finally
            {
                this._downloadSemaphore.Release();
            }
        }

        #endregion


        public BackupDomain(DateTime lastRunDate)
        {
            this._lastRunDate = lastRunDate;            this._logger = ApplicationLogger.GetInstance();
            this._consoleHeartbeat = new BackupConsoleHeartbeat(this._logger);
            this._reportService = new BackupRunReportService(this._logger);
            this._largeGdocFallbackService = new BackupLargeGdocFallbackService(this._logger);

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
            this._nonRetryableFailures = 0;
            this._failedDownloadsReportPath = string.Empty;
            this._largeGdocFallbackAttempted = 0;
            this._largeGdocFallbackFailed = 0;
            this._largeGdocFallbackUnresolvedCount = 0;
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
                var fallbackResult = await this._largeGdocFallbackService
                    .ExecuteForFailedDownloadsAsync(service, credential, this._failedDownloadList.ToList(), this._runId)
                    .ConfigureAwait(false);
                if (fallbackResult != null)
                {
                    this._largeGdocFallbackAttempted = fallbackResult.Attempted;
                    this._largeGdocFallbackFailed = fallbackResult.Failed;
                    this._largeGdocFallbackUnresolvedCount = fallbackResult.Items
                        .Where(item => string.Equals(item.Status, "failed", StringComparison.OrdinalIgnoreCase)
                                       && !string.IsNullOrWhiteSpace(item.SourceFileId))
                        .Select(item => item.SourceFileId)
                        .Distinct(StringComparer.Ordinal)
                        .Count();
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
                var duration = DateTime.UtcNow - this._startRunDate;
                var runStatus = runCompleted ? "completed" : "aborted";

                // Best-effort report generation must run even when backup aborts early.
                // This ensures failed-export artifacts are still available for manual follow-up.
                try
                {
                    if (string.IsNullOrWhiteSpace(this._failedDownloadsReportPath))
                    {
                        this._failedDownloadsReportPath = this._reportService.WriteFailedDownloadsReportAndLogSummary(
                            this._failedDownloadList?.ToList() ?? new List<FailedDownload>(),
                            this._runId,
                            this._startRunDate,
                            runStatus,
                            duration,
                            this._foldersVisited,
                            this._filesDiscovered,
                            this._downloadAttempts,
                            this._initialFailuresDetected,
                            this._nonRetryableFailures,
                            this._largeGdocFallbackAttempted,
                            this._largeGdocFallbackFailed,
                            this._largeGdocFallbackUnresolvedCount);
                    }
                }
                catch (Exception ex)
                {
                    this._logger.Error(
                    $"RunId [{this._runId}] failed while generating end-of-run reports.",
                        ex);
                }

                this._logger.Info(
                    $"Backup run {runStatus}. RunId [{this._runId}], Duration [{duration:dd\\.hh\\:mm\\:ss\\:fff}], FoldersVisited [{this._foldersVisited}], FilesDiscovered [{this._filesDiscovered}], DownloadCalls [{this._downloadAttempts}], InitialFailures [{this._initialFailuresDetected}], NonRetryableFailures [{this._nonRetryableFailures}], FailedDownloadsReport [{this._failedDownloadsReportPath}], LargeGdocFallbackAttempted [{this._largeGdocFallbackAttempted}], LargeGdocFallbackFailed [{this._largeGdocFallbackFailed}], LargeGdocFallbackUnresolvedCount [{this._largeGdocFallbackUnresolvedCount}].");

                this._downloadSemaphore?.Dispose();
                this._consoleHeartbeat.Stop();
                this._logger.ClearContext("RunId");
            }
        }
    }
}

