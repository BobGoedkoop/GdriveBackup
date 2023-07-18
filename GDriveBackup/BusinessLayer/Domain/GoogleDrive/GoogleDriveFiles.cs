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

namespace GDriveBackup.BusinessLayer.Domain.GoogleDrive
{
    // Documents and sheets, not images
    public abstract class GoogleDriveFiles
    {
        private readonly DriveService _service;
        private readonly ConsoleLogger _logger;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        /// <see cref="https://developers.google.com/drive/api/guides/search-files"/>
        protected IList<Google.Apis.Drive.v3.Data.File> List( string mimeType )
        {
            var request = this._service.Files.List();

            var lastRunDateIso8601 = Config.GetInstance().LastRunDateTime.ToIso8601();
            request.Q = $"trashed = false "  // Never return trashed files
                        + "and " 
                        + $"mimeType = '{mimeType}' " // Only return specific files: gdoc, sheet, ...
                        //+ "and " 
                        //+ $"modifiedTime > '{lastRunDateIso8601}' " // Default time zone is UTC
                ;
            //request.Fields = "ModifiedTime";

            var results = request.ExecuteAsync().Result;

            this._logger.Log( "\n\n" );
            this._logger.Log( results.Files );

            return results.Files;
        }

        protected async void Download( string mimeType )
        {
            this._logger.Log( "\n\n" );

            // Get list of files
            var files = this.List( mimeType );

            if ( files.Count <= 0 )
            {
                return;
            }

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
        }


        protected GoogleDriveFiles( DriveService service )
        {
            this._service = service ?? throw new ArgumentNullException( nameof( service ) );

            this._logger = ConsoleLogger.GetInstance();
        }


        public abstract void Download();
    }
}
