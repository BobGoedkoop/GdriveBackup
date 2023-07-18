using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GDocToPDF.BusinessLayer.Extensions;
using GDocToPDF.BusinessLayer.Types;
using Google.Apis.Drive.v3;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDocToPDF.BusinessLayer.Domain.GoogleDrive
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
