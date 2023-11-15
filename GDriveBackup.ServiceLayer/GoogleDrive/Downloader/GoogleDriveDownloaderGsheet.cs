using System;
using GDriveBackup.Core.Constants;
using Google.Apis.Drive.v3;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    public class GoogleDriveDownloaderGsheet: GoogleDriveDownloader
    {
        public GoogleDriveDownloaderGsheet( DriveService service ) 
            : base( service )
        {
        }

        public override void DownloadFile(string localPath, Google.Apis.Drive.v3.Data.File file)
        {
            //base.DoDownloadFile(localPath, FileExtensionConstants.Pdf, MimeTypeConstants.ApplicationPdf, file);
            base.Logger.Warn( $"No download implementation for GSheet [{file.Name}]." );
            return;
        }

    public override void DownloadAll(  DateTime since )
        {
            base.DoDownloadAll( MimeTypeConstants.Gdoc, since );
        }
    }
}
