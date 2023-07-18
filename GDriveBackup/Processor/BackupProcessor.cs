using System;
using GDriveBackup.BusinessLayer.Domain.GoogleDrive;
using GDriveBackup.Crosscutting.Configuration;

namespace GDriveBackup.Processor
{
    public class BackupProcessor
    {
        public BackupProcessor() { }

        public void DoBackup()
        {
            var gAuth = new GoogleDriveAuthenticate();
            var credential = gAuth.Authenticate();

            var gService = new GoogleDriveService();
            var service = gService.GetService(credential);

            var gFiles = new GoogleDriveFilesGdoc(service);
            gFiles.Download();

            var localStorage = Config.GetInstance();
            localStorage.LastRunDateTime = DateTime.UtcNow;
            localStorage.Persist();
        }
    }
}
