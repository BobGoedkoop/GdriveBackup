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
using Google.Apis.Auth.OAuth2;
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
        public bool ResolvedByAutoSplit { get; set; } = false;
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
        private string _largeGdocSplitExecutionPath;
        private long _largeGdocSplitCandidates;
        private long _largeGdocAutoSplitAttempted;
        private long _largeGdocAutoSplitSucceeded;
        private long _largeGdocAutoSplitFailed;
        private bool _largeGdocAutoSplitDryRun;
        private readonly string _largeGdocAutoSplitFileId;
        private readonly BackupConsoleHeartbeat _consoleHeartbeat;


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

        private async Task ExecuteLargeGdocAutoSplitAsync(DriveService service, GoogleCredential credential)
        {
            var settings = ApplicationSettings.GetInstance();
            this._largeGdocAutoSplitDryRun = false;

            var candidates = this._failedDownloadList
                .Where(f => !f.ResolvedByAutoSplit
                            && GoogleDriveDownloaderGdocForLargeFiles.IsOversizedGdocExportFailure(
                                f.GDriveFile,
                                f.LocelMimeType,
                                f.IsRetryable,
                                f.FailureReason))
                .Select(f => new GoogleDriveLargeGdocAutoSplitter.LargeGdocSplitCandidate
                {
                    SourceFile = f.GDriveFile,
                    LocalPath = f.LocalPath,
                    FailureReason = f.FailureReason,
                    AttemptedExportMimeType = f.LocelMimeType
                })
                .GroupBy(c => c.SourceFile.Id)
                .Select(group => group.Last())
                .ToList();

            if (!string.IsNullOrWhiteSpace(this._largeGdocAutoSplitFileId))
            {
                candidates = candidates
                    .Where(c => string.Equals(c.SourceFile.Id, this._largeGdocAutoSplitFileId, StringComparison.Ordinal))
                    .ToList();

                this._logger.Warn(
                    $"RunId [{this._runId}] large GDoc auto-split file filter active: FileId [{this._largeGdocAutoSplitFileId}], CandidatesAfterFilter [{candidates.Count}].");
            }

            if (!candidates.Any())
            {
                this._logger.Info($"RunId [{this._runId}] no oversized GDoc candidates for auto-split execution.");
                return;
            }

            this._logger.Warn(
                $"RunId [{this._runId}] starting large GDoc auto-split execution for [{candidates.Count}] candidates.");

            var splitter = new GoogleDriveLargeGdocAutoSplitter(service, credential, this._logger);
            var result = await splitter.ExecuteAsync(candidates, this._runId).ConfigureAwait(false);

            this._largeGdocSplitExecutionPath = result.ReportPath ?? string.Empty;
            this._largeGdocAutoSplitAttempted = result.Attempted;
            this._largeGdocAutoSplitSucceeded = result.Succeeded;
            this._largeGdocAutoSplitFailed = result.Failed;
            this._largeGdocAutoSplitDryRun = result.DryRun;

            if (result.ResolvedSourceFileIds != null && result.ResolvedSourceFileIds.Count > 0)
            {
                var resolvedIds = new HashSet<string>(result.ResolvedSourceFileIds, StringComparer.Ordinal);
                foreach (var failedDownload in this._failedDownloadList.Where(f => f.GDriveFile != null && resolvedIds.Contains(f.GDriveFile.Id)))
                {
                    failedDownload.ResolvedByAutoSplit = true;
                }
            }

            this._logger.Warn(
                $"RunId [{this._runId}] large GDoc auto-split execution finished: Attempted [{result.Attempted}], Succeeded [{result.Succeeded}], Failed [{result.Failed}], DryRun [{result.DryRun}], Report [{result.ReportPath}].");
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
                .Where(f => !f.ResolvedByAutoSplit
                            && !f.IsRetryable
                            && f.GDriveFile != null
                            && !string.IsNullOrWhiteSpace(f.GDriveFile.Id))
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
            var candidates = this._failedDownloadList
                .Where(f => !f.ResolvedByAutoSplit
                            && GoogleDriveDownloaderGdocForLargeFiles.IsOversizedGdocExportFailure(
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


        public BackupDomain(DateTime lastRunDate, string autoSplitFileId = "")
        {
            this._lastRunDate = lastRunDate;
            this._largeGdocAutoSplitFileId = (autoSplitFileId ?? string.Empty).Trim();
            this._logger = ApplicationLogger.GetInstance();
            this._consoleHeartbeat = new BackupConsoleHeartbeat(this._logger);

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

                var sourceFile = new GoogleDriveFile(service).GetFile(fileId);
                if (sourceFile == null)
                {
                    this._logger.Error($"Split-only run failed: file not found for FileId [{fileId}].");
                    return;
                }

                if (!string.Equals(sourceFile.MimeType, MimeTypeConstants.Gdoc, StringComparison.OrdinalIgnoreCase))
                {
                    this._logger.Warn(
                        $"Split-only run file is not a Google Doc. FileId [{fileId}], MimeType [{sourceFile.MimeType}].");
                }

                var splitter = new GoogleDriveLargeGdocAutoSplitter(service, credential, this._logger);
                var candidate = new GoogleDriveLargeGdocAutoSplitter.LargeGdocSplitCandidate
                {
                    SourceFile = sourceFile,
                    // In split-only mode we do not have an original folder context; export to root export path.
                    LocalPath = ApplicationSettings.GetInstance().ExportPath,
                    FailureReason = "PDF export too large for Drive export API.",
                    AttemptedExportMimeType = MimeTypeConstants.ApplicationPdf
                };

                var result = await splitter.ExecuteAsync(
                        new List<GoogleDriveLargeGdocAutoSplitter.LargeGdocSplitCandidate> { candidate },
                        runId)
                    .ConfigureAwait(false);

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
                await this.ExecuteLargeGdocAutoSplitAsync(service, credential).ConfigureAwait(false);
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
                    $"Backup run {runStatus}. RunId [{this._runId}], Duration [{duration:dd\\.hh\\:mm\\:ss\\:fff}], FoldersVisited [{this._foldersVisited}], FilesDiscovered [{this._filesDiscovered}], DownloadCalls [{this._downloadAttempts}], InitialFailures [{this._initialFailuresDetected}], RetryAttempts [{this._retryAttempts}], RetryCallsCompleted [{this._retryCompletedCalls}], NonRetryableFailures [{this._nonRetryableFailures}], FailedExportsReport [{this._failedExportsReportPath}], LargeGdocSplitCandidates [{this._largeGdocSplitCandidates}], LargeGdocSplitPlanReport [{this._largeGdocSplitPlanPath}], LargeGdocSplitExecutionReport [{this._largeGdocSplitExecutionPath}], LargeGdocAutoSplitAttempted [{this._largeGdocAutoSplitAttempted}], LargeGdocAutoSplitSucceeded [{this._largeGdocAutoSplitSucceeded}], LargeGdocAutoSplitFailed [{this._largeGdocAutoSplitFailed}], LargeGdocAutoSplitDryRun [{this._largeGdocAutoSplitDryRun}].");

                this._downloadSemaphore?.Dispose();
                this._consoleHeartbeat.Stop();
                this._logger.ClearContext("RunId");
            }
        }
    }
}
