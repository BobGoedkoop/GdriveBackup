using System;
using System.Threading.Tasks;
using GDriveBackup.Core.Constants;
using Google.Apis.Drive.v3;
using File = Google.Apis.Drive.v3.Data.File;

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    public class GoogleDriveDownloaderPdf : GoogleDriveDownloader
    {
        public GoogleDriveDownloaderPdf(DriveService service)
            : base(service)
        {
        }

        public override Task DownloadFileAsync(string localPath, File file)
        {
            return base.DoDownloadFileAsync(
                localPath,
                FileExtensionConstants.Pdf,
                MimeTypeConstants.ApplicationPdf,
                file);
        }

        public override void DownloadAll(DateTime since)
        {
            base.DoDownloadAll(MimeTypeConstants.ApplicationPdf, since);
        }
    }
}
