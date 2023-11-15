using System;
using System.IO;
using GDriveBackup.Core.Constants;
using GDriveBackup.Core.Extensions;
using GDriveBackup.Crosscutting.Logging;
using GDriveBackup.ServiceLayer.GoogleDrive.Files;
using Google.Apis.Drive.v3;

// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.FolderWalker
{
    /// <summary>
    /// </summary>
    /// List all files on Google Drive
    /// <see cref=" https://www.daimto.com/list-all-files-on-google-drive/"/>
    /// List files (a folder is also a "file") in a folder.
    /// <see cref="https://stackoverflow.com/questions/60177954/google-drive-api-v3-is-there-anyway-to-list-of-files-and-folders-from-a-root-fo"/>
    public class GoogleDriveFolderWalker
    {
        private readonly DriveService _service;
        private readonly ConsoleLogger _logger;

        private void DoFolderHandler(  WalkerCurrentFolder currentFolder )
        {
            if ( this.OnFolder == null )
            {
                this._logger.Info( $"Folder walker is not assigned a client OnFolder handler." );
                return;
            }

            try
            {
                this.OnFolder( this._service, currentFolder );
            }
            catch ( Exception ex )
            {
                this._logger.Error($"OnFolder of client caused an exception.", ex);
            }
        }

        private void DoFinishedHandler()
        {
            if (this.OnFinished == null)
            {
                this._logger.Info($"Folder walker is not assigned a client OnFinished handler.");
                return;
            }

            try
            {
                this.OnFinished(this._service);
            }
            catch (Exception ex)
            {
                this._logger.Error( $"OnFinished of client caused an exception.", ex);
            }

        }

        private void DoWalk( WalkerCurrentFolder currentFolder, int folderDepth )
        {
            this._logger.Debug( $"Walk Google Drive folder [{currentFolder.GDriveFile.Name}]; depth [{folderDepth}].");

            this.DoFolderHandler(  currentFolder );


            var googleDriveFolder = new GoogleDriveFolder( this._service );
            var gDriveFolderList = googleDriveFolder.GetSubFolders( currentFolder.GDriveFile.Id );


            // Recurse to a deeper level.
            folderDepth++;

            foreach ( var gDriveFolder in gDriveFolderList.Files )
            {
                var subFolder = new WalkerCurrentFolder()
                {
                    GDriveFile = gDriveFolder,
                    LocalFullPath = Path.Combine( currentFolder.LocalFullPath, gDriveFolder.Name.ReplaceInvalidCharacters() )
                };

                this.DoWalk(
                    subFolder,
                    folderDepth
                );
            }

        }



        public GoogleDriveFolderWalker(DriveService service)
        {
            this._service = service ?? throw new ArgumentNullException(nameof(service));

            this._logger = ConsoleLogger.GetInstance();

            this.OnFolder = null;
            this.OnFinished = null;
        }

        public delegate void OnFolderDelegate(DriveService service, WalkerCurrentFolder currentFolder);
        public OnFolderDelegate OnFolder { get; set; }

        public delegate void OnFinishedDelegate(DriveService service);
        public OnFinishedDelegate OnFinished { get; set; }


        public void Walk()
        {
            // Collect the current, start, folder
            var gDriveFolder = new GoogleDriveFolder(this._service);
            var ropeMarksFolder = gDriveFolder.GetFolder(GoogleDriveFileIdConstants.FolderRopeMarks);

            var currentFolder = new WalkerCurrentFolder()
            {
                GDriveFile = ropeMarksFolder,
                LocalFullPath = Path.Combine( ApplicationConstants.ExportPath, ropeMarksFolder.Name )
            };

            // Start walking!
            this.DoWalk(
                currentFolder,
                0 
            );

            this.DoFinishedHandler();
        }
    }
}
