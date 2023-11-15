using GDriveBackup.Core.Constants;
using GDriveBackup.Crosscutting.Logging;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Downloader
{
    public class GoogleDriveDownloaderFactory
    {
        private readonly ConsoleLogger _logger;

        #region Singleton

        private static GoogleDriveDownloaderFactory _instance = null;

        protected GoogleDriveDownloaderFactory()
        {
            this._logger = ConsoleLogger.GetInstance();
        }

        public static GoogleDriveDownloaderFactory GetInstance()
        {
            if ( _instance == null )
            {
                _instance = new GoogleDriveDownloaderFactory();
            }
            return _instance;
        }

        #endregion

        public GoogleDriveDownloader GetDownloader( DriveService service, File file )
        {
            GoogleDriveDownloader downloader = null;

            if ( file.MimeType == MimeTypeConstants.Gdoc )
            {
                downloader = new GoogleDriveDownloaderGdoc(service);
            }
            else if (file.MimeType == MimeTypeConstants.Gsheet)
            {
                downloader = new GoogleDriveDownloaderGsheet(service);
            }
            else if (file.MimeType == MimeTypeConstants.TxtPlain)
            {
                downloader = new GoogleDriveDownloaderTxt(service);
            }
            else
            {
                this._logger.Warn( $"No downloader available for file of type [{file.MimeType}] (Id [{file.Id}], Name [{file.Name}])." );
            }
            return downloader;
        }
    }
}
