using Google.Apis.Drive.v3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GDriveBackup.Core.Constants;
using GDriveBackup.Crosscutting.Logging;
using GDriveBackup.Core.Extensions;
using GDriveBackup.Crosscutting.Configuration;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Requests;

namespace GDriveBackup.ServiceLayer.GoogleDrive
{

    /// <summary>
    ///
    ///
    /// LIST ALL FILES ON GOOGLE DRIVE
    /// https://www.daimto.com/list-all-files-on-google-drive/
    ///
    /// 
    /// </summary>
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

        public GoogleFolderWalker(DriveService service)
        {
            this._service = service ?? throw new ArgumentNullException(nameof(service));

            this._logger = ConsoleLogger.GetInstance();
        }

        public void Walk()
        {
            // By default this will return ALL files 
            var request = this._service.Files.List();

            request.Q = $"trashed = false " // Never return trashed files
                        + "and "
                        // Retrieve folders in root folder
                        + "mimeType = 'application/vnd.google-apps.folder'" //"and 'root' in parents"
                ;
            this._logger.Log($"Request Q [{request.Q}].");

            request.PageSize = 100; // not picked up


            // https://www.daimto.com/list-all-files-on-google-drive/
            // By using a page streamer I don't have to worry about the nextpagetoken
            var pageStreamer = new PageStreamer<File, FilesResource.ListRequest, FileList, string>(
                (req, token) => request.PageToken = token,
                response => response.NextPageToken,
                response => response.Files);

            var allFiles = new FileList();
            allFiles.Files = new List<File>();

            // This will retrieve one by one, not blocks
            foreach (var result in pageStreamer.Fetch(request))
            {
                allFiles.Files.Add(result);
                Console.WriteLine($"Retrieved files [{allFiles.Files.Count}].");
            }



            // The max default amount of files is 100
            // if there are more we need to get the next "block" of 100, etc.
            // for this you use the nextPageToken in the result
            //var fileList = request.ExecuteAsync().Result;

            Console.WriteLine( $"ETag [{allFiles.ETag}]." );
            Console.WriteLine( $"IncompleteSearch [{allFiles.IncompleteSearch}]." );
            Console.WriteLine( $"Kind [{allFiles.Kind}]." );
            Console.WriteLine( $"NextPageToken [{allFiles.NextPageToken}]." );
            Console.WriteLine($"File count [{allFiles.Files.Count}].");

            foreach ( var file in allFiles.Files )
            {
                Console.WriteLine( "\nFile:");
                Console.WriteLine( $"DriveId [{file.DriveId}].");
                Console.WriteLine( $"MimeType [{file.MimeType}]." );
                Console.WriteLine($"Name [{file.Name}].");
            }
        }
    }
}
