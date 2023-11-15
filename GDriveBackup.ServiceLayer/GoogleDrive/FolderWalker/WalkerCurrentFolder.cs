using Google.Apis.Drive.v3.Data;

namespace GDriveBackup.ServiceLayer.GoogleDrive.FolderWalker
{
    public class WalkerCurrentFolder
    {
        public WalkerCurrentFolder()
        {

        }

        public File GDriveFile { get; set; }
        public string LocalFullPath { get; set; }
    }
}
