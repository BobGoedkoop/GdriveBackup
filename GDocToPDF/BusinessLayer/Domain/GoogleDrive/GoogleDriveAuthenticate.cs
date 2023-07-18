using GDocToPDF.BusinessLayer.Types;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;

// ReSharper disable StringLiteralTypo

namespace GDocToPDF.BusinessLayer.Domain.GoogleDrive
{
    // Authenticate using OAuth 2.0
    public class GoogleDriveAuthenticate
    {

        public GoogleCredential Authenticate()
        {
            string[] scopes = new string[] { DriveService.ScopeConstants.Drive };

            var credential = GoogleCredential
                .FromFile( ApplicationConstants.JsonCredentialsPath )
                .CreateScoped(scopes);
            
            return credential;
        }
    }
}
