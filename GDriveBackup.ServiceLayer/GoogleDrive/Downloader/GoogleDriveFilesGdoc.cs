using System;
using GDriveBackup.Core.Constants;
using Google.Apis.Drive.v3;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    // Documents and sheets, not images
    public class GoogleDriveFilesGdoc: GoogleDriveFiles
    {
        public GoogleDriveFilesGdoc( DriveService service ) : base( service )
        {

        }

        public override void Download(DateTime lastRunDate)
        {
            base.DoDownload(MimeTypeConstants.Gdoc, lastRunDate );
        }
    }
}
