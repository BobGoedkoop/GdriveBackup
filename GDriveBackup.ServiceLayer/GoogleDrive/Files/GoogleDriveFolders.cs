using System;
using System.Collections.Generic;
using GDriveBackup.Core.Constants;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Requests;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Files
{
    public class GoogleDriveFolder: GoogleDriveFile
    {

        public GoogleDriveFolder( DriveService service ) : base( service)
        {
        }


        public File GetFolder( string gDriveFileId )
        {
            var file = base.GetFile( gDriveFileId );
            
            base.Logger.Log( file );

            return !base.IsFolder( file) ? null : file;
        }

        public FileList GetSubFolders(string parentGDriveFileId)
        {
            // By default this will return ALL files 
            var request = base.Service.Files.List();

            request.Q = $"trashed = false " // Never return trashed files
                        + "and "
                        + $"mimeType = '{MimeTypeConstants.Gfolder}' " // Retrieve folders only
                        + "and "
                        //+ "'root' in parents" // Retrieve folders in 'root'
                        + $"'{parentGDriveFileId}' in parents" // Retrieve folders in 'parentGDriveFileId'
                ;
            base.Logger.Log($"Request Q [{request.Q}].");

            request.PageSize = 100; // not picked up


            // https://www.daimto.com/list-all-files-on-google-drive/
            // By using a page streamer I don't have to worry about the nextpagetoken
            var pageStreamer = new PageStreamer<File, FilesResource.ListRequest, FileList, string>(
                (req, token) => request.PageToken = token,
                response => response.NextPageToken,
                response => response.Files);



            var folders = new FileList
            {
                Files = new List<File>()
            };

            // This will retrieve one by one, not blocks
            foreach (var result in pageStreamer.Fetch(request))
            {
                folders.Files.Add(result);

                base.Logger.Log(result);
            }
            base.Logger.Log($"Retrieved [{folders.Files.Count}] folders.");

            return folders;
        }

    }
}
