using GDriveBackup.Core.Constants;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

// ReSharper disable StringLiteralTypo

namespace GDriveBackup.BusinessLayer.Domain.GoogleDrive
{
    // Authenticate using OAuth 2.0
    public class GoogleDriveService
    {

        public DriveService GetService( GoogleCredential credential )
        {
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = $"{ApplicationConstants.ApplicationName} {ApplicationConstants.ApplicationVersion}",
            });

            return service;
        }
    }
}
