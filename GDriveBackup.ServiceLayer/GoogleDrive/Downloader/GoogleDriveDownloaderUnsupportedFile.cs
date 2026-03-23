using System;
using Google.Apis.Drive.v3;
using System.Threading.Tasks;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    public class GoogleDriveDownloaderUnsupportedFile : GoogleDriveDownloader
    {
        public GoogleDriveDownloaderUnsupportedFile( DriveService service ) 
            : base( service )
        {
        }

        public override Task DownloadFileAsync( string localPath, Google.Apis.Drive.v3.Data.File file )
        {
            base.Logger.Warn( $"No downloader for: Localpath [{localPath}] GDrive file [{file.Id}], [{file.Name}]." );
            return Task.CompletedTask;
        }

        public override void DownloadAll( DateTime since )
        {
            base.Logger.Warn($"No downloader.");
        }
    }
}
