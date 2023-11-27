using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using GDriveBackup.Core.Constants;
using GDriveBackup.Core.Extensions;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;
using Google.Apis.Drive.v3;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    public abstract class GoogleDriveDownloader
    {
        private readonly DriveService _service;


        private void DoFailedHandler(string localPath, string localExt, string localMimeType, Google.Apis.Drive.v3.Data.File file)
        {
            if (this.OnFailed == null)
            {
                this.Logger.Info($"Google Drive downloader is not assigned a client OnFailed handler.");
                return;
            }

            try
            {
                this.OnFailed( localPath, localExt, localMimeType, file );
            }
            catch (Exception ex)
            {
                this.Logger.Error($"OnFailed of client caused an exception.", ex);
            }
        }
        
        
        protected readonly IApplicationLogger Logger;

        
        protected GoogleDriveDownloader(DriveService service)
        {
            this._service = service ?? throw new ArgumentNullException(nameof(service));

            this.Logger = ApplicationLogger.GetInstance();
        }

        protected async void DoDownloadFile( string localPath, string localExt, string localMimeType, Google.Apis.Drive.v3.Data.File file )
        {
            var dstFullPath = Path.Combine(
                localPath,
                Path.ChangeExtension(
                    file.Name.ToValidFileName().ReplaceSpaceCharacters(),
                    localExt
                )
            );
            this.Logger.Info($"Download [{file.Name}] to [{dstFullPath}].");

            try
            {

                var getRequest = this._service.Files.Export(
                    file.Id,
                    /*Export converts to:*/ localMimeType
                );

                using (var filestream = new FileStream(dstFullPath, FileMode.Create, FileAccess.Write))
                {
                    // This will overwrite an existing file by the same name.
                    await getRequest.DownloadAsync(filestream);
                }

            }
            catch ( Exception ex )
            {
                this.Logger.Error( $"Downloading [{file.Name}] to [{dstFullPath}] failed.", ex );
                this.DoFailedHandler(localPath, localExt, localMimeType, file);
            }
        }

        public abstract void DownloadFile( string localPath, Google.Apis.Drive.v3.Data.File file );





        private string BuildQuery(string mimeType, DateTime lastRunDate)
        {
            var query = $"trashed = false " // Never return trashed files
                        + "and "
                        + $"mimeType = '{mimeType}' " // Only return specific files: gdoc, sheet, ...
                ;

            if (lastRunDate != Config.DefaultLastRunDate)
            {
                query += "and "
                      + $"modifiedTime > '{lastRunDate.ToIso8601()}' " // Default time zone is UTC
                ;
            }

            this.Logger.Debug($"Query [{query}].");

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
            //request.Fields = "ModifiedTime";

            var results = request.ExecuteAsync().Result;

            this.Logger.Debug(results.Files);

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


        public delegate void OnFailedDelegate(string localPath, string localExt, string localMimeType, Google.Apis.Drive.v3.Data.File file);
        public OnFailedDelegate OnFailed { get; set; }
    }
}
