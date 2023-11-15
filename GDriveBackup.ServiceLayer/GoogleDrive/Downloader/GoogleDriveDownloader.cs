using System;
using System.Collections.Generic;
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
        
        
        protected readonly ConsoleLogger Logger;

        
        protected GoogleDriveDownloader(DriveService service)
        {
            this._service = service ?? throw new ArgumentNullException(nameof(service));

            this.Logger = ConsoleLogger.GetInstance();
        }

        protected async void DoDownloadFile( string localPath, string localExt, string localMimeType, Google.Apis.Drive.v3.Data.File file )
        {
            var validFileName = file.Name.ToValidFileName();
            var dstFilePath = Path.Combine(localPath, $"{validFileName}{localExt}"); 

            this.Logger.Info($"Download [{file.Name}] to [{dstFilePath}].");


            var getRequest = this._service.Files.Export(
                file.Id,
                /*Export converts to:*/ localMimeType 
            );

            using (var filestream = new FileStream(dstFilePath, FileMode.Create, FileAccess.Write))
            {
                // This will overwrite an existing file by the same name.
                await getRequest.DownloadAsync(filestream);
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

            this.Logger.Info($"Query [{query}].");

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

            this.Logger.Debug("\n");
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
                this.DownloadFile(ApplicationConstants.ExportPath, file);
            }

            this.Logger.Info($"Downloaded [{files.Count}] [{mimeType}] files.\n");
        }

        /// <summary>
        /// This will download all files of a specific (mime)type, i.e. gdoc, gsheet, ...
        /// Derived classes will determine the (mime)type.
        /// </summary>
        /// <param name="since"></param>
        public abstract void DownloadAll( DateTime since );
    }
}
