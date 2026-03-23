using GDriveBackup.Core.Constants;
using GDriveBackup.Crosscutting.Configuration;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using System;

// ReSharper disable StringLiteralTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Service
{
    // Authenticate using OAuth 2.0
    public class GoogleDriveService
    {

        public DriveService GetService( GoogleCredential credential )
        {
            // Large exports can legitimately run longer than default timeouts.
            // Keep this configurable so slow-but-valid requests are less likely to cancel.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = $"{ApplicationSettings.GetInstance().ApplicationName} {ApplicationSettings.GetInstance().ApplicationVersion}",
            });
            service.HttpClient.Timeout = TimeSpan.FromSeconds(ApplicationSettings.GetInstance().DriveApiRequestTimeoutSeconds);

            return service;
        }
    }
}
