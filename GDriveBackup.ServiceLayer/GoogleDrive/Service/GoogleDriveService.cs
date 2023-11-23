using GDriveBackup.Core.Constants;
using GDriveBackup.Crosscutting.Configuration;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

// ReSharper disable StringLiteralTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Service
{
    // Authenticate using OAuth 2.0
    public class GoogleDriveService
    {

        public DriveService GetService( GoogleCredential credential )
        {
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = $"{ApplicationSettings.GetInstance().ApplicationName} {ApplicationSettings.GetInstance().ApplicationVersion}",
            });

            return service;
        }
    }
}
