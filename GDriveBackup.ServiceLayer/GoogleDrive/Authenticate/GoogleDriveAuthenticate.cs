using GDriveBackup.Core.Constants;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;

// ReSharper disable StringLiteralTypo

namespace GDriveBackup.ServiceLayer.GoogleDrive.Authenticate
{
    // Authenticate using OAuth 2.0
    public class GoogleDriveAuthenticate
    {

        public GoogleCredential Authenticate()
        {
            var logger = ApplicationLogger.GetInstance();
            logger.Trace( $">> Authenticate.");

            string[] scopes = new string[]
            {
                DriveService.ScopeConstants.Drive
            };

            var credential = GoogleCredential
                .FromFile( ApplicationSettings.GetInstance().JsonCredentialsPath )
                .CreateScoped(scopes);

            logger.Trace($"<< Authenticate.");
            return credential;
        }
    }
}
