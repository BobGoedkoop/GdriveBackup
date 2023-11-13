using Google.Apis.Drive.v3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GDriveBackup.Core.Constants;
using GDriveBackup.Core.Extensions;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Requests;
using Newtonsoft.Json;
using File = Google.Apis.Drive.v3.Data.File;

// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive
{

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

        private FileList GetFoldersIn( string folderId )
        {
            // By default this will return ALL files 
            var request = this._service.Files.List();

            request.Q = $"trashed = false " // Never return trashed files
                        + "and "
                        + "mimeType = 'application/vnd.google-apps.folder' " // Retrieve folders only
                        + "and "
                        //+ "'root' in parents" // Retrieve folders in 'root'
                        + $"'{folderId}' in parents" // Retrieve folders in 'folderId'
                ;
            this._logger.Log( $"Request Q [{request.Q}]." );

            request.PageSize = 100; // not picked up


            // https://www.daimto.com/list-all-files-on-google-drive/
            // By using a page streamer I don't have to worry about the nextpagetoken
            var pageStreamer = new PageStreamer<File, FilesResource.ListRequest, FileList, string>(
                ( req, token ) => request.PageToken = token,
                response => response.NextPageToken,
                response => response.Files );



            var folders = new FileList
            {
                Files = new List<File>()
            };

            // This will retrieve one by one, not blocks
            foreach ( var result in pageStreamer.Fetch( request ) )
            {
                folders.Files.Add( result );

                this._logger.Log( result );
            }
            this._logger.Log($"Retrieved [{folders.Files.Count}] folders.");

            return folders;
        }

        private void DoWalk( string parentPath, string folderId, int folderDepth )
        {
            //Console.WriteLine( $"Walk folder [{folderId}]; parentPath [{parentPath}], level [{folderLevel}].");

            if (folderDepth > 6 )
            {
                // We've reached the end of the folder depth
                // Create this folder!

                try
                {
                    Directory.CreateDirectory(parentPath);
                }
                catch ( PathTooLongException ex )
                {
                    Console.WriteLine( ex );
                    throw;
                }
                return;
            }

            var folders = this.GetFoldersIn( folderId );


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

            foreach ( var folder in folders.Files )
            {
                //Console.WriteLine("\nFile:");
                //Console.WriteLine($"Id [{file.Id}].");
                //Console.WriteLine($"DriveId [{file.DriveId}].");
                //Console.WriteLine($"MimeType [{file.MimeType}].");
                //Console.WriteLine($"Name [{file.Name}].");

                var path = Path.Combine( parentPath, folder.Name.ReplaceInvalidCharacters() );
                //_logger.Log($"{parentPath}{Path.DirectorySeparatorChar}{folder.Name}");
                _logger.Log($"{path}");

                this.DoWalk(
                    //$"{parentPath}{Path.DirectorySeparatorChar}{folder.Name}",
                    path.ToString(),
                    folder.Id,
                    folderDepth
                );
            }

        }

        public GoogleFolderWalker(DriveService service)
        {
            this._service = service ?? throw new ArgumentNullException(nameof(service));

            this._logger = ConsoleLogger.GetInstance();
        }

        public void Walk()
        {
            //
            // Do get the RopeMArks folder and append to Export Path
            //



            this.DoWalk(
                ApplicationConstants.ExportPath, 
                GoogleDriveFileIdConstants.FolderRopeMarks, 
                0 
            );
        }
    }
}
