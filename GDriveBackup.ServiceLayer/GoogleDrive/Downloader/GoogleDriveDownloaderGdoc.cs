using System;
using GDriveBackup.Core.Constants;
using Google.Apis.Drive.v3;
using System.Threading.Tasks;

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

        public override Task DownloadFileAsync( string localPath, Google.Apis.Drive.v3.Data.File file )
        {
            return base.DoDownloadFileAsync( localPath, FileExtensionConstants.Pdf, MimeTypeConstants.ApplicationPdf, file );
        }

        public override void DownloadAll( DateTime since )
        {
            base.DoDownloadAll( MimeTypeConstants.Gdoc, since );
        }
    }
}
