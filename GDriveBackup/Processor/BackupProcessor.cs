using System;
using GDriveBackup.BusinessLayer.Domain.GoogleDrive;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;

namespace GDriveBackup.Processor
{
    public class BackupProcessor
    {
        public BackupProcessor() { }

        public void DoBackup()
        {
            var logger = ConsoleLogger.GetInstance();
            logger.Log( "\n>> Do Google Drive backup.");

            var gAuth = new GoogleDriveAuthenticate();
            var credential = gAuth.Authenticate();

            var gService = new GoogleDriveService();
            var service = gService.GetService(credential);

            var gFiles = new GoogleDriveFilesGdoc(service);
            gFiles.Download();

            var gSheets = new GoogleDriveFilesGSheet( service );
            gSheets.Download();


            logger.Log(">> Update config.");

            var config = Config.GetInstance();
            config.LastRunDate = DateTime.UtcNow;
            config.Persist();

            logger.Log("<< Update config.");


            logger.Log("<< Do Google Drive backup.");
        }
    }
}
