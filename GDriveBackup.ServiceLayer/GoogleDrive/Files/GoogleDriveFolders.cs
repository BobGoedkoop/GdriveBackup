using System;
using System.Collections.Generic;
using GDriveBackup.Core.Constants;
using GDriveBackup.Core.Extensions;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.DataLayer.Repository;
using Google;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Requests;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Files
{
    public class GoogleDriveFolder : GoogleDriveFile
    {
        private static bool IsTransientException(Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                if (current is TaskCanceledException)
                {
                    return true;
                }

                if (current is GoogleApiException googleApiException)
                {
                    var statusCode = (int)googleApiException.HttpStatusCode;
                    if (statusCode == 429 || statusCode == 500 || statusCode == 502 || statusCode == 503 || statusCode == 504)
                    {
                        return true;
                    }
                }

                current = current.InnerException;
            }

            return false;
        }

        private T ExecuteWithTransientRetry<T>(Func<T> action, string actionDescription)
        {
            var settings = ApplicationSettings.GetInstance();
            var maxAttempts = settings.DriveApiTransientRetryCount;
            var baseDelayMs = settings.DriveApiRetryBaseDelayMs;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return action();
                }
                catch (Exception ex) when (attempt < maxAttempts && IsTransientException(ex))
                {
                    var delayMs = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    base.Logger.Warn(
                        $"{actionDescription} failed with transient error (attempt {attempt}/{maxAttempts}). Retrying in {delayMs} ms.");
                    Thread.Sleep(delayMs);
                }
            }

            // Last attempt should return/throw from inside the loop; this is a defensive fallback.
            return action();
        }

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
            return this.ExecuteWithTransientRetry(() =>
            {
                // By default this will return ALL files
                var request = base.Service.Files.List();

                request.Q = $"trashed = false " // Never return trashed files
                            + "and "
                            + $"mimeType = '{MimeTypeConstants.Gfolder}' " // Retrieve folders only
                            + "and "
                            + $"'{parentGDriveFileId}' in parents" // Retrieve folders in 'parentGDriveFileId'
                    ;
                base.Logger.Debug( $"Request Q [{request.Q}]." );

                // Larger pages reduce API round trips on large folder trees.
                request.PageSize = 1000;
                // Request only fields needed by traversal/downloader selection.
                request.Fields = "nextPageToken, files(id, name, mimeType, parents)";

                // By using a page streamer we don't have to handle nextPageToken manually.
                var pageStreamer = new PageStreamer<File, FilesResource.ListRequest, FileList, string>(
                    ( req, token ) => request.PageToken = token,
                    response => response.NextPageToken,
                    response => response.Files );

                var folders = new FileList
                {
                    Files = new List<File>()
                };

                foreach ( var result in pageStreamer.Fetch( request ) )
                {
                    base.Logger.Debug(result);
                    folders.Files.Add( result );
                }

                base.Logger.Debug( $"Retrieved [{folders.Files.Count}] folders." );
                return folders;
            }, $"List subfolders for parent [{parentGDriveFileId}]");
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
            return this.ExecuteWithTransientRetry(() =>
            {
                var request = base.Service.Files.List();
                request.Q = this.BuildQueryForGetFilesInFolder(parentGDriveFileId, since);
                // Larger pages reduce API round trips while walking folders.
                request.PageSize = 1000;
                // Keep payload lean; we only need identifiers and download metadata.
                request.Fields = "nextPageToken, files(id, name, mimeType, modifiedTime, parents)";

                // By using a page streamer we don't have to handle nextPageToken manually.
                var pageStreamer = new PageStreamer<File, FilesResource.ListRequest, FileList, string>(
                    ( req, token ) => request.PageToken = token,
                    response => response.NextPageToken,
                    response => response.Files );

                var files = new FileList
                {
                    Files = new List<File>()
                };

                foreach ( var result in pageStreamer.Fetch( request ) )
                {
                    base.Logger.Debug(result);
                    files.Files.Add( result );
                }

                base.Logger.Debug( $"Retrieved [{files.Files.Count}] files." );
                return files;
            }, $"List files in folder [{parentGDriveFileId}]");
        }


        public FileList GetFilesInFolder( string parentGDriveFileId, DateTime since )
        {
            return DoGetFilesInFolder(parentGDriveFileId, since);
        }

    }

}
