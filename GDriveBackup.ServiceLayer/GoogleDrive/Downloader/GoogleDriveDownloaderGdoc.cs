using System;
using GDriveBackup.Core.Constants;
using Google.Apis.Drive.v3;
using File = Google.Apis.Drive.v3.Data.File;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    public class GoogleDriveDownloaderGdoc: GoogleDriveDownloader
    {
        public GoogleDriveDownloaderGdoc( DriveService service ) 
            : base( service )
        {
        }

        public override void DownloadFile( string localPath, File file )
        {
            base.DoDownloadFile( localPath, FileExtensionConstants.Pdf, MimeTypeConstants.ApplicationPdf, file );
        }

        public override void DownloadAll( DateTime since )
        {
            base.DoDownloadAll( MimeTypeConstants.Gdoc, since );
        }
    }
}
