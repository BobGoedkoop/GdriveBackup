using System;
using GDriveBackup.Core.Constants;
using Google.Apis.Drive.v3;
using File = Google.Apis.Drive.v3.Data.File;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    public class GoogleDriveDownloaderTxt: GoogleDriveDownloader
    {
        public GoogleDriveDownloaderTxt( DriveService service ) 
            : base( service )
        {
        }

        public override void DownloadFile( string localPath, File file )
        {
            base.DoDownloadFile( localPath, FileExtensionConstants.Txt, MimeTypeConstants.TxtPlain, file );
        }

        public override void DownloadAll( DateTime since )
        {
            base.DoDownloadAll( MimeTypeConstants.Gdoc, since );
        }
    }
}
