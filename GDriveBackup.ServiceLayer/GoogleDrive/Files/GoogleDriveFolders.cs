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
using System.Threading;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Files
{
    public class GoogleDriveFolder : GoogleDriveFile
    {
        private static readonly Lazy<SemaphoreSlim> ListRequestSemaphore =
            new Lazy<SemaphoreSlim>(() =>
            {
                var maxConcurrent = ApplicationSettings.GetInstance().DriveApiMaxConcurrentListRequests;
                return new SemaphoreSlim(maxConcurrent, maxConcurrent);
            });

        private T ExecuteWithListThrottle<T>(Func<T> action)
        {
            var semaphore = ListRequestSemaphore.Value;
            semaphore.Wait();
            try
            {
                return action();
            }
            finally
            {
                semaphore.Release();
            }
        }

        public GoogleDriveFolder( DriveService service ) 
            : base( service )
        {
        }


        public File GetFolder( string gDriveFileId )
        {
            var file = base.GetFile( gDriveFileId );

            base.Logger.Trace($"File: Name [{file?.Name}], MimeType [{file?.MimeType}], Id [{file?.Id}].");

            return !base.IsFolder( file ) ? null : file;
        }

        public FileList GetSubFolders( string parentGDriveFileId )
        {
            return this.ExecuteWithListThrottle(() =>
            {
                // By default this will return ALL files
                var request = base.Service.Files.List();

                request.Q = $"trashed = false " // Never return trashed files
                            + "and "
                            + $"mimeType = '{MimeTypeConstants.Gfolder}' " // Retrieve folders only
                            + "and "
                            + $"'{parentGDriveFileId}' in parents" // Retrieve folders in 'parentGDriveFileId'
                    ;
                base.Logger.Trace( $"Request Q [{request.Q}]." );

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
                    base.Logger.Trace($"File: Name [{result?.Name}], MimeType [{result?.MimeType}], Id [{result?.Id}].");
                    folders.Files.Add( result );
                }

                base.Logger.Debug( $"Retrieved [{folders.Files.Count}] folders." );
                return folders;
            });
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

            base.Logger.Trace( $"Query [{qry}]." );
            return qry;
        }

        private FileList DoGetFilesInFolder( string parentGDriveFileId, DateTime since )
        {
            return this.ExecuteWithListThrottle(() =>
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
                    base.Logger.Trace($"File: Name [{result?.Name}], MimeType [{result?.MimeType}], Id [{result?.Id}].");
                    files.Files.Add( result );
                }

                base.Logger.Debug( $"Retrieved [{files.Files.Count}] files." );
                return files;
            });
        }


        public FileList GetFilesInFolder( string parentGDriveFileId, DateTime since )
        {
            return DoGetFilesInFolder(parentGDriveFileId, since);
        }

    }

}
