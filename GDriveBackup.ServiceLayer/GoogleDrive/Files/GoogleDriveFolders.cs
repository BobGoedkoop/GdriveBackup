using System;
using System.Collections.Generic;
using GDriveBackup.Core.Constants;
using GDriveBackup.Core.Extensions;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.DataLayer.Repository;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Requests;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Files
{
    public class GoogleDriveFolder : GoogleDriveFile
    {

        public GoogleDriveFolder( DriveService service ) 
            : base( service )
        {
        }


        public File GetFolder( string gDriveFileId )
        {
            var file = base.GetFile( gDriveFileId );

            base.Logger.Debug( file );

            return !base.IsFolder( file ) ? null : file;
        }

        public FileList GetSubFolders( string parentGDriveFileId )
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
            base.Logger.Debug( $"Request Q [{request.Q}]." );

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
                base.Logger.Debug(result);
                folders.Files.Add( result );
            }

            base.Logger.Debug( $"Retrieved [{folders.Files.Count}] folders." );

            return folders;
        }


        private string BuildQueryForGetFilesInFolder( string parentGDriveFileId, DateTime since )
        {
            var qry = $"trashed = false " // Never return trashed files
                      + "and "
                      + $"mimeType != '{MimeTypeConstants.Gfolder}' " // Retrieve all files except folders
                      + "and "
                      + $"'{parentGDriveFileId}' in parents " // Retrieve files in 'parentGDriveFileId'
                ;

            var runDateRepo = new RunDateRepository();
            if ( since != runDateRepo.DefaultLastRunDate )
            {
                qry += "and "
                       + $"modifiedTime > '{since.ToIso8601()}' " // Default time zone is UTC
                    ;
            }

            base.Logger.Debug( $"Query [{qry}]." );
            return qry;
        }

        private FileList DoGetFilesInFolder( string parentGDriveFileId, DateTime since )
        {
            var request = base.Service.Files.List();
            request.Q = this.BuildQueryForGetFilesInFolder(parentGDriveFileId, since);
            request.PageSize = 100; // not picked up

            
            // https://www.daimto.com/list-all-files-on-google-drive/
            // By using a page streamer I don't have to worry about the nextpagetoken
            var pageStreamer = new PageStreamer<File, FilesResource.ListRequest, FileList, string>(
                ( req, token ) => request.PageToken = token,
                response => response.NextPageToken,
                response => response.Files );



            var files = new FileList
            {
                Files = new List<File>()
            };

            // This will retrieve one by one, not blocks
            foreach ( var result in pageStreamer.Fetch( request ) )
            {
                base.Logger.Debug(result);
                files.Files.Add( result );
            }

            base.Logger.Debug( $"Retrieved [{files.Files.Count}] files." );

            return files;
        }


        public FileList GetFilesInFolder( string parentGDriveFileId, DateTime since )
        {
            return DoGetFilesInFolder(parentGDriveFileId, since);
        }

    }

}
