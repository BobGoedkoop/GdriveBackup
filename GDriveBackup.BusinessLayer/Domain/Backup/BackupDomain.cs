using GDriveBackup.Crosscutting.Configuration;
using System;
using System.Collections.Generic;
using GDriveBackup.Crosscutting.Logging;
using GDriveBackup.ServiceLayer.GoogleDrive.Authenticate;
using GDriveBackup.ServiceLayer.GoogleDrive.Downloader;
using GDriveBackup.ServiceLayer.GoogleDrive.Service;
using GDriveBackup.ServiceLayer.GoogleDrive.FolderWalker;
using System.IO;
using System.Management;
using System.Threading;
using GDriveBackup.BusinessLayer.Domain.Run;
using GDriveBackup.DataLayer.Repository;
using GDriveBackup.ServiceLayer.GoogleDrive.Files;
using Google.Apis.Drive.v3;

// ReSharper disable IdentifierTypo

namespace GDriveBackup.BusinessLayer.Domain.Backup
{
    public class FailedDownload
    {
        public string LocalPath { get; set; } = string.Empty;
        public string LocalExt { get; set; } = string.Empty;
        public string LocelMimeType { get; set; } = string.Empty;
        public Google.Apis.Drive.v3.Data.File GDriveFile { get; set; } = null;
    }


    public class BackupDomain
    {
        private readonly DateTime _lastRunDate;
        private readonly IApplicationLogger _logger;

        private DateTime _startRunDate;
        private List<FailedDownload> _failedDownloadList;


        #region Private section

        private void DoDownloadFailedHandler( string localPath, string localExt, string localMimeType, Google.Apis.Drive.v3.Data.File file )
        {
            this._failedDownloadList.Add( new FailedDownload()
            {
                LocalPath = localPath,
                LocalExt = localExt,
                LocelMimeType = localMimeType,
                GDriveFile = file
            } );
        }


        /// <summary>
        /// </summary>
        /// <param name="state"></param>
        private void FolderHandlerThread( object state )
        {
            this._logger.Trace(">> FolderHandlerThread( object state )");

            var objectArray = state as object[];
            var service = (DriveService)objectArray[0];
            var currentFolder = (WalkerCurrentFolder)objectArray[1];

            try
            {

                var gDriveFolder = new GoogleDriveFolder(service);
                var fileList = gDriveFolder.GetFilesInFolder(currentFolder.GDriveFile.Id, this._lastRunDate);

                foreach (var file in fileList.Files)
                {
                    var downloader = GoogleDriveDownloaderFactory
                        .GetInstance()
                        .GetDownloader(service, file);

                    downloader.OnFailed = DoDownloadFailedHandler;
                    downloader.DownloadFile(currentFolder.LocalFullPath, file);
                }

            }
            catch (Exception ex)
            {
                this._logger.Error($"Download failed.", ex);
                //throw;  todo Add to failed download handler if not already done.
            }

            this._logger.Trace("<< FolderHandlerThread( object state )");
        }

        private void DoFolderHandler(DriveService service, WalkerCurrentFolder currentFolder)
        {
            this._logger.Trace($"DoFolderHandler( currentFolder [{currentFolder.LocalFullPath}] [{currentFolder.GDriveFile.Name}] )");

            try
            {
                // This will create the directory if it does not exist yet.
                Directory.CreateDirectory(currentFolder.LocalFullPath);
            }
            catch (PathTooLongException ex)
            {
                this._logger.Error( $"Path too long [{currentFolder.LocalFullPath}].", ex);
                throw;
            }

            ThreadPool.QueueUserWorkItem( FolderHandlerThread, new object[]{ service, currentFolder } );
        }

        private void DoFinishedHandler( DriveService service )
        {
            this._logger.Trace( $"DoFinishedHandler(  )");

            this._logger.Debug(">> Update config.");

            var runDateDomain = new RunDateDomain();
            runDateDomain.LastRunDate = this._startRunDate;

            this._logger.Debug("<< Update config.");

            var duration = DateTime.UtcNow - this._startRunDate;
            this._logger.Info( $"Backup finished and took [{duration:dd\\.hh\\:mm\\:ss\\:fff}]." );
        }

        #endregion


        public BackupDomain( DateTime lastRunDate)
        {
            this._lastRunDate = lastRunDate;
            this._logger = ApplicationLogger.GetInstance();
        }

        public void Start()
        {
            this._logger.Trace(">> Do Google Drive backup.");

            this._startRunDate = DateTime.UtcNow;
            this._failedDownloadList = new List<FailedDownload>();


            var gAuth = new GoogleDriveAuthenticate();
            var credential = gAuth.Authenticate();

            var gService = new GoogleDriveService();
            var service = gService.GetService(credential);


            var walker = new GoogleDriveFolderWalker(service)
            {
                OnFolder = DoFolderHandler,
                OnFinished = DoFinishedHandler
            };

            walker.Walk();

            foreach ( var failedDownload in this._failedDownloadList )
            {
                try
                {
                    this._logger.Info($"Retry download ([{failedDownload.GDriveFile.Name}] to [{failedDownload.LocalPath}])");

                    GoogleDriveDownloaderFactory.GetInstance()
                        .GetDownloader(service, failedDownload.GDriveFile)
                        .DownloadFile(failedDownload.LocalPath, failedDownload.GDriveFile);
                }
                catch ( Exception ex )
                {
                    this._logger.Error(
                        $"The retry of the failed download ([{failedDownload.GDriveFile.Name}] to [{failedDownload.LocalPath}]) failed.",
                        ex );
                }
            }


            this._logger.Trace("<< Do Google Drive backup.");
        }
    }
}
