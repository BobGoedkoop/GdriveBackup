using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Policy;
using GDriveBackup.Core.Constants;
using GDriveBackup.Core.Extensions;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;
using Google.Apis.Drive.v3;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive
{
    // Documents and sheets, not images
    public abstract class GoogleDriveFiles
    {
        private readonly DriveService _service;
        private readonly ConsoleLogger _logger;


        private string BuildQuery( string mimeType, DateTime lastRunDate )
        {
            var query = $"trashed = false " // Never return trashed files
                        + "and "
                        + $"mimeType = '{mimeType}' " // Only return specific files: gdoc, sheet, ...
                ;

            if ( lastRunDate != Config.DefaultLastRunDate )
            {
                query += "and "
                         + $"modifiedTime > '{lastRunDate.ToIso8601()}' " // Default time zone is UTC
                    ;

            }

            this._logger.Log($"Request Q [{query}].");

            return query;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mimeType"></param>
        /// <param name="lastRunDate"></param>
        /// <returns></returns>
        /// <see cref="https://developers.google.com/drive/api/guides/search-files"/>
        protected IList<Google.Apis.Drive.v3.Data.File> List( string mimeType, DateTime lastRunDate  )
        {
            var request = this._service.Files.List();

            request.Q = this.BuildQuery( 
                mimeType, 
                lastRunDate 
            );
            //request.Fields = "ModifiedTime";

            var results = request.ExecuteAsync().Result;

            this._logger.Log( "\n" );
            this._logger.Log( results.Files );

            return results.Files;
        }

        protected async void DoDownload( string mimeType, DateTime lastRunDate)
        {
            this._logger.Log( "\n" );

            // Get list of files
            var files = this.List( mimeType, lastRunDate  );

            if ( files.Count <= 0 )
            {
                this._logger.Log($"No [{mimeType}] files to download.\n");
                return;
            }

            this._logger.Log($"Downloading [{files.Count}] [{mimeType}] files.\n");

            // Do Download
            foreach ( var file in files )
            {
                var validFileName = file.Name.ToValidFileName();
                var dstFilePath =
                    $"{ApplicationConstants.ExportPath}{validFileName}{FileExtensionConstants.Pdf}";

                this._logger.Log( $"Download [{file.Name}] to [{dstFilePath}]." );


                var getRequest = this._service.Files.Export(
                    file.Id,
                    /*Export converts to:*/ MimeTypeConstants.ApplicationPdf
                );

                using ( var filestream = new FileStream( dstFilePath, FileMode.Create, FileAccess.Write ) )
                {
                    // This will overwrite an existing file by the same name.
                    await getRequest.DownloadAsync( filestream );
                }
            }

            this._logger.Log($"Downloaded all [{mimeType}] files.\n");
        }


        protected GoogleDriveFiles( DriveService service )
        {
            this._service = service ?? throw new ArgumentNullException( nameof( service ) );

            this._logger = ConsoleLogger.GetInstance();
        }


        public abstract void Download(DateTime lastRunDate);
    }
}
