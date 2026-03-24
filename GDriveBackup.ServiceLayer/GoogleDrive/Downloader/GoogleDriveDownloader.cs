using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using GDriveBackup.Core.Constants;
using GDriveBackup.Core.Extensions;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;
using GDriveBackup.DataLayer.Repository;
using Google;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Security.Cryptography;
using System.Text;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    public abstract class GoogleDriveDownloader
    {
        private const int MaxSafeWindowsPathLength = 240;
        private readonly DriveService _service;
        private static readonly ConcurrentDictionary<string, object> DestinationFileLocks =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static readonly ThreadLocal<Random> RetryJitterRandom =
            new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));


        private void DoFailedHandler(
            string localPath,
            string localExt,
            string localMimeType,
            Google.Apis.Drive.v3.Data.File file,
            bool isRetryable,
            string failureReason)
        {
            if (this.OnFailed == null)
            {
                this.Logger.Warn($"Google Drive downloader has no OnFailed handler; failure details will not be queued.");
                return;
            }

            try
            {
                this.OnFailed(
                    localPath,
                    localExt,
                    localMimeType,
                    file,
                    isRetryable,
                    failureReason
                );
            }
            catch (Exception ex)
            {
                this.Logger.Error($"OnFailed of client caused an exception.", ex);
            }
        }
        
        
        private bool IsNonRetryableTooLargeExport(Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                if (current is GoogleApiException googleApiException)
                {
                    var message = googleApiException.Message ?? string.Empty;
                    if (googleApiException.HttpStatusCode == HttpStatusCode.Forbidden
                        && message.IndexOf("too large to be exported", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                current = current.InnerException;
            }

            return false;
        }

        private bool IsTransientException(Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                if (current is TaskCanceledException)
                {
                    return true;
                }

                if (current is GoogleApiException googleApiException)
                {
                    var statusCode = (int)googleApiException.HttpStatusCode;
                    if (statusCode == 429 || statusCode == 500 || statusCode == 502 || statusCode == 503 || statusCode == 504)
                    {
                        return true;
                    }
                }

                current = current.InnerException;
            }

            return false;
        }

        private async Task DownloadAndPromoteFileAsync(
            string dstFullPath,
            string localMimeType,
            Google.Apis.Drive.v3.Data.File file)
        {
            var settings = ApplicationSettings.GetInstance();
            var maxAttempts = settings.DriveApiTransientRetryCount;
            var baseDelayMs = settings.DriveApiRetryBaseDelayMs;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var destinationDirectory = Path.GetDirectoryName(dstFullPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                var tmpFullPath = $"{dstFullPath}.partial.{Guid.NewGuid():N}";
                try
                {
                    // Use media download for non-Google-native files (same source/target mime type).
                    // Export is only valid for Google Docs Editors types and fails for files like text/plain.
                    var useDirectMediaDownload =
                        string.Equals(file?.MimeType, localMimeType, StringComparison.OrdinalIgnoreCase);

                    // Write to a temporary file first. This prevents a failed/partial download
                    // from truncating a previously good backup file to 0 bytes.
                    using (var filestream = new FileStream(tmpFullPath, FileMode.CreateNew, FileAccess.Write))
                    {
                        IDownloadProgress downloadProgress;
                        if (useDirectMediaDownload)
                        {
                            var getRequest = this._service.Files.Get(file.Id);
                            downloadProgress = await getRequest.DownloadAsync(filestream).ConfigureAwait(false);
                        }
                        else
                        {
                            var exportRequest = this._service.Files.Export(file.Id, localMimeType);
                            downloadProgress = await exportRequest.DownloadAsync(filestream).ConfigureAwait(false);
                        }
                        filestream.Flush();

                        // A request can complete without throwing, yet still produce an empty file.
                        // Validate both transfer status and resulting size before promoting output.
                        if (downloadProgress.Status != DownloadStatus.Completed || filestream.Length <= 0)
                        {
                            var progressDetails =
                                $"Status [{downloadProgress.Status}], Bytes [{filestream.Length}], DownloadedBytes [{downloadProgress.BytesDownloaded}].";

                            if (downloadProgress.Exception != null)
                            {
                                throw new InvalidOperationException(
                                    $"Download validation failed. {progressDetails}",
                                    downloadProgress.Exception);
                            }

                            throw new InvalidOperationException(
                                $"Download validation failed. {progressDetails}");
                        }
                    }

                    // Multiple Drive files can normalize to the same local filename.
                    // Serialize final replacement per destination to avoid clobber races.
                    var fileLock = DestinationFileLocks.GetOrAdd(dstFullPath, _ => new object());
                    lock (fileLock)
                    {
                        if (File.Exists(dstFullPath))
                        {
                            File.Delete(dstFullPath);
                        }

                        File.Move(tmpFullPath, dstFullPath);
                    }

                    return;
                }
                catch (Exception ex) when (attempt < maxAttempts && this.IsTransientException(ex))
                {
                    var delayMs = CalculateBackoffDelayWithJitter(baseDelayMs, attempt);
                    this.Logger.Warn(
                        $"Transient export failure for [{file.Name}] (Id [{file.Id}]) attempt {attempt}/{maxAttempts}. Retrying in {delayMs} ms.",
                        new Dictionary<string, object>
                        {
                            ["FileId"] = file?.Id ?? string.Empty,
                            ["MimeType"] = file?.MimeType ?? string.Empty,
                            ["RetryAttempt"] = attempt,
                            ["RetryMax"] = maxAttempts
                        });
                    await Task.Delay(delayMs).ConfigureAwait(false);
                }
                finally
                {
                    if (File.Exists(tmpFullPath))
                    {
                        File.Delete(tmpFullPath);
                    }
                }
            }
        }

        private static int CalculateBackoffDelayWithJitter(int baseDelayMs, int attempt)
        {
            var exponentialDelay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
            var jitterRange = Math.Max(1, baseDelayMs / 2);
            var jitterMs = RetryJitterRandom.Value.Next(0, jitterRange + 1);
            var delay = exponentialDelay + jitterMs;
            return delay > 0 ? delay : baseDelayMs;
        }

        protected readonly IApplicationLogger Logger;

        
        protected GoogleDriveDownloader(DriveService service)
        {
            this._service = service ?? throw new ArgumentNullException(nameof(service));

            this.Logger = ApplicationLogger.GetInstance();
        }

        protected async Task DoDownloadFileAsync( string localPath, string localExt, string localMimeType, Google.Apis.Drive.v3.Data.File file )
        {
            var dstFullPath = this.BuildDestinationPath(localPath, localExt, file);
            this.Logger.Debug($"Download [{file.Name}] to [{dstFullPath}].");

            try
            {
                await this.DownloadAndPromoteFileAsync(dstFullPath, localMimeType, file);

                this.Logger.Debug($"Download completed [{file.Name}] => [{dstFullPath}].");

            }
            catch ( Exception ex )
            {
                var isTooLargeExport = this.IsNonRetryableTooLargeExport(ex);
                var isRetryable = true;
                var failureReason = ex.Message;

                // Some files cannot be exported by Drive due to size limits.
                // Mark those as non-retryable to avoid guaranteed-fail retries.
                if (isTooLargeExport
                    && localMimeType == MimeTypeConstants.ApplicationPdf
                    && file.MimeType == MimeTypeConstants.Gdoc)
                {
                    isRetryable = false;
                    failureReason = "PDF export too large for Drive export API.";
                }

                if (isTooLargeExport)
                {
                    isRetryable = false;
                }

                this.Logger.Error(
                    $"Downloading [{file.Name}] (Id [{file.Id}], MimeType [{file.MimeType}]) to [{dstFullPath}] failed.",
                    ex );

                this.DoFailedHandler(
                    localPath,
                    localExt,
                    localMimeType,
                    file,
                    isRetryable,
                    failureReason
                );
            }
        }

        private string BuildDestinationPath(string localPath, string localExt, Google.Apis.Drive.v3.Data.File file)
        {
            var safeFileName = Path.ChangeExtension(
                file.Name.ToValidFileName().ReplaceSpaceCharacters(),
                localExt);
            var fullPath = Path.Combine(localPath, safeFileName);
            if (fullPath.Length <= MaxSafeWindowsPathLength)
            {
                return fullPath;
            }

            // Deep folder structures + recursive split suffixes can exceed Windows path limits.
            // Shorten only the filename and keep the folder structure intact.
            var extension = Path.GetExtension(safeFileName);
            var stem = Path.GetFileNameWithoutExtension(safeFileName);
            var stableHash = this.ComputeStableHash($"{file?.Id}|{safeFileName}").Substring(0, 10);

            var availableNameLength = Math.Max(12, MaxSafeWindowsPathLength - localPath.Length - 1);
            var reservedLength = extension.Length + 1 + stableHash.Length;
            var maxStemLength = Math.Max(4, availableNameLength - reservedLength);
            if (stem.Length > maxStemLength)
            {
                stem = stem.Substring(0, maxStemLength);
            }

            var shortenedFileName = $"{stem}_{stableHash}{extension}";
            var shortenedFullPath = Path.Combine(localPath, shortenedFileName);
            if (shortenedFullPath.Length > MaxSafeWindowsPathLength)
            {
                shortenedFileName = $"{stableHash}{extension}";
                shortenedFullPath = Path.Combine(localPath, shortenedFileName);
            }

            this.Logger.Warn(
                $"Path too long; using shortened output filename for [{file?.Name}] (Id [{file?.Id}]).");
            return shortenedFullPath;
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

        public abstract Task DownloadFileAsync( string localPath, Google.Apis.Drive.v3.Data.File file );





        private string BuildQuery(string mimeType, DateTime lastRunDate)
        {
            var query = $"trashed = false " // Never return trashed files
                        + "and "
                        + $"mimeType = '{mimeType}' " // Only return specific files: gdoc, sheet, ...
                ;

            var runDateRepo = new RunDateRepository();
            if (lastRunDate != runDateRepo.DefaultLastRunDate)
            {
                query += "and "
                      + $"modifiedTime > '{lastRunDate.ToIso8601()}' " // Default time zone is UTC
                ;
            }

            this.Logger.Trace($"Query [{query}].");

            return query;
        }

        /// <summary>
        /// </summary>
        /// <param name="mimeType"></param>
        /// <param name="lastRunDate"></param>
        /// <returns></returns>
        /// <see cref="https://developers.google.com/drive/api/guides/search-files"/>
        protected IList<Google.Apis.Drive.v3.Data.File> List(string mimeType, DateTime lastRunDate)
        {
            var request = this._service.Files.List();

            request.Q = this.BuildQuery( mimeType, lastRunDate );
            // Larger pages reduce API round trips when listing files by mime type.
            request.PageSize = 1000;
            // Only retrieve metadata required for naming/export decisions.
            request.Fields = "nextPageToken, files(id, name, mimeType, modifiedTime, parents)";

            var results = request.Execute();

            this.Logger.Trace($"List query returned [{results?.Files?.Count ?? 0}] files.");

            return results.Files;
        }

        protected void DoDownloadAll(string mimeType, DateTime since)
        {
            var files = this.List(mimeType, since);

            if (files.Count <= 0)
            {
                this.Logger.Info($"No [{mimeType}] files to download.");
                return;
            }

            this.Logger.Info($"Downloading [{files.Count}] [{mimeType}] files.\n");

            // Do Download
            foreach (var file in files)
            {
                this.DownloadFile(ApplicationSettings.GetInstance().ExportPath, file);
            }

            this.Logger.Info($"Downloaded [{files.Count}] [{mimeType}] files.\n");
        }

        /// <summary>
        /// This will download all files of a specific (mime)type, i.e. gdoc, gsheet, ...
        /// Derived classes will determine the (mime)type.
        /// </summary>
        /// <param name="since"></param>
        public abstract void DownloadAll( DateTime since );

        public void DownloadFile( string localPath, Google.Apis.Drive.v3.Data.File file )
        {
            this.DownloadFileAsync( localPath, file ).GetAwaiter().GetResult();
        }


        public delegate void OnFailedDelegate(
            string localPath,
            string localExt,
            string localMimeType,
            Google.Apis.Drive.v3.Data.File file,
            bool isRetryable,
            string failureReason
        );
        public OnFailedDelegate OnFailed { get; set; }
    }
}
