using System;
using GDocToPDF.BusinessLayer.Domain.GoogleDrive;
using GDocToPDF.BusinessLayer.Domain.LocalStorage;
using GDocToPDF.BusinessLayer.Domain.Logging;
using GDocToPDF.BusinessLayer.Types;

// ReSharper disable StringLiteralTypo

namespace GDocToPDF
{
    class Program
    {
        static void Main( string[] args )
        {
            var logger = ConsoleLogger.GetInstance();
            logger.Log( $"{ApplicationConstants.ApplicationName} {ApplicationConstants.ApplicationVersion}" );

            var gAuth = new GoogleDriveAuthenticate();
            var credential = gAuth.Authenticate();

            var gService = new GoogleDriveService();
            var service = gService.GetService( credential );

            var gFiles = new GoogleDriveFilesGdoc( service );
            gFiles.Download();

            var localStorage = LocalStorageDomain.GetInstance();
            localStorage.LastRunDateTime = DateTime.UtcNow;
            localStorage.Persist();

            logger.Log( $"\n\n{ApplicationConstants.ApplicationPressAnyKey}\n\n" );
            Console.ReadKey();
        }
    }
}
