using System;
using GDriveBackup.Core.Constants;
using GDriveBackup.Crosscutting.Logging;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Files
{
    public class GoogleDriveFile
    {
        protected readonly DriveService Service;
        protected readonly IApplicationLogger Logger;


        public GoogleDriveFile( DriveService service )
        {
            this.Service = service ?? throw new ArgumentNullException( nameof( service ) );

            this.Logger = ApplicationLogger.GetInstance();
        }


        public bool IsFolder( File file )
        {
            return ( file.MimeType == MimeTypeConstants.Gfolder );
        }

        public File GetFile( string gDriveFileId )
        {
            var request = this.Service.Files.Get( gDriveFileId );
            var file = request.ExecuteAsync().Result;
            return file;
        }
    }
}
