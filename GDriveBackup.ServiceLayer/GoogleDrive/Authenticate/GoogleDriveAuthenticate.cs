using GDriveBackup.Core.Constants;
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
            var logger = ConsoleLogger.GetInstance();
            logger.Debug( $">> Authenticate.");

            string[] scopes = new string[]
            {
                DriveService.ScopeConstants.Drive
            };

            var credential = GoogleCredential
                .FromFile( ApplicationConstants.JsonCredentialsPath )
                .CreateScoped(scopes);

            logger.Debug($"<< Authenticate.");
            return credential;
        }
    }
}
