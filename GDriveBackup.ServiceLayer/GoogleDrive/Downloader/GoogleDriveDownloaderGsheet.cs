using System;
using GDriveBackup.Core.Constants;
using Google.Apis.Drive.v3;
using System.Threading.Tasks;

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

        public override Task DownloadFileAsync(string localPath, Google.Apis.Drive.v3.Data.File file)
        {
            // Export Google Sheets as Excel workbook for offline backup.
            return base.DoDownloadFileAsync(
                localPath,
                FileExtensionConstants.Xlsx,
                MimeTypeConstants.ApplicationXlsx,
                file);
        }

        public override void DownloadAll(  DateTime since )
        {
            base.DoDownloadAll( MimeTypeConstants.Gsheet, since );
        }
    }
}
