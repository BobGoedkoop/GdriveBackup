using GDriveBackup.Core.Constants;
using Google.Apis.Drive.v3;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.BusinessLayer.Domain.GoogleDrive
{
    // Documents and sheets, not images
    public class GoogleDriveFilesGdoc: GoogleDriveFiles
    {
        public GoogleDriveFilesGdoc( DriveService service ) : base( service )
        {

        }

        public override void Download()
        {
            base.Download(MimeTypeConstants.Gdoc);
        }
    }
}
