using System;
using System.Collections.Generic;
using System.IO;
using GDriveBackup.Core.Constants;
using GDriveBackup.Core.Extensions;
using GDriveBackup.Crosscutting.Logging;
using GDriveBackup.ServiceLayer.GoogleDrive.Files;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Requests;
using File = Google.Apis.Drive.v3.Data.File;

// ReSharper disable IdentifierTypo

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

    /// <summary>
    /// 
    /// </summary>
    /// List all files on Google Drive
    /// <see cref=" https://www.daimto.com/list-all-files-on-google-drive/"/>
    /// List files (a folder is also a "file") in a folder.
    /// <see cref="https://stackoverflow.com/questions/60177954/google-drive-api-v3-is-there-anyway-to-list-of-files-and-folders-from-a-root-fo"/>
    public class GoogleFolderWalker
    {
        private readonly DriveService _service;
        private readonly ConsoleLogger _logger;

        private void DoFolderHandler( WalkerCurrentFolder currentFolder )
        {
            if ( this.OnFolder == null )
            {
                return;
            }

            try
            {
                this.OnFolder( currentFolder );
            }
            catch ( Exception ex )
            {
                this._logger.Log( ex );
            }
        }

        //private  static void PrettyPrint(DriveService service, FileList list, string indent)
        //{
        //    foreach (var item in list.Files.OrderBy(a => a.Name))
        //    {
        //        Console.WriteLine( $"{indent}|-{item.Name}" );

        //        if (item.MimeType == "application/vnd.google-apps.folder")
        //        {
        //            var ChildrenFiles = ListAll(service, new FilesListOptionalParms { Q = string.Format("('{0}' in parents)", item.Id), PageSize = 1000 });
        //            PrettyPrint(service, ChildrenFiles, indent + "  ");
        //        }
        //    }
        //}


        private void DoWalk( WalkerCurrentFolder currentFolder, int folderDepth )
        {
            //Console.WriteLine( $"Walk folder [{folderId}]; parentPath [{parentPath}], level [{folderLevel}].");

            this.DoFolderHandler(  currentFolder );

            if (folderDepth > 6 )
            {
                // We've reached the end of the folder depth
                return;
            }

            var gDriveFolders = new GoogleDriveFolder( this._service );
            var gDriveFolderList = gDriveFolders.GetSubFolders( currentFolder.GDriveFile.Id );


            // The max default amount of files is 100
            // if there are more we need to get the next "block" of 100, etc.
            // for this you use the nextPageToken in the result
            //var fileList = request.ExecuteAsync().Result;

            //Console.WriteLine($"ETag [{allFiles.ETag}].");
            //Console.WriteLine($"IncompleteSearch [{allFiles.IncompleteSearch}].");
            //Console.WriteLine($"Kind [{allFiles.Kind}].");
            //Console.WriteLine($"NextPageToken [{allFiles.NextPageToken}].");
            //Console.WriteLine($"File count [{allFiles.Files.Count}].");

            // Recurse to a deeper level.
            folderDepth++;

            foreach ( var gDriveFolder in gDriveFolderList.Files )
            {
                //Console.WriteLine("\nFile:");
                //Console.WriteLine($"Id [{file.Id}].");
                //Console.WriteLine($"DriveId [{file.DriveId}].");
                //Console.WriteLine($"MimeType [{file.MimeType}].");
                //Console.WriteLine($"Name [{file.Name}].");

                var subFolder = new WalkerCurrentFolder()
                {
                    GDriveFile = gDriveFolder,
                    LocalFullPath = Path.Combine( currentFolder.LocalFullPath, gDriveFolder.Name.ReplaceInvalidCharacters() )
                };
                //_logger.Log($"{parentPath}{Path.DirectorySeparatorChar}{folder.Name}");
                //_logger.Log($"{path}");

                this.DoWalk(
                    subFolder,
                    folderDepth
                );
            }

        }



        public GoogleFolderWalker(DriveService service)
        {
            this._service = service ?? throw new ArgumentNullException(nameof(service));

            this._logger = ConsoleLogger.GetInstance();

            this.OnFolder = null;
        }


        public void Walk()
        {
            //
            // Collect the current, start, folder
            //
            var gDriveFolder = new GoogleDriveFolder(this._service);
            var ropeMarksFolder = gDriveFolder.GetFolder(GoogleDriveFileIdConstants.FolderRopeMarks);

            var currentFolder = new WalkerCurrentFolder()
            {
                GDriveFile = ropeMarksFolder,
                LocalFullPath = Path.Combine( ApplicationConstants.ExportPath, ropeMarksFolder.Name )
            };

            this.DoWalk(
                currentFolder,
                0 
            );
        }


        public  delegate void OnFolderDelegate(WalkerCurrentFolder currentFolder);
        public OnFolderDelegate OnFolder { get; set; }
    }
}
