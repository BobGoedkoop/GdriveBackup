using System;
// ReSharper disable CommentTypo

namespace GDriveBackup.Core.Extensions
{
    public static class DateTimeExtensions
    {
        public static string ToIso8601( this DateTime srcDateTime )
        {
            // https://developers.google.com/gmail/markup/reference/datetime-formatting
            /*
                DateTime values are expected to be in the ISO 8601 format, 
                for example '2013-02-14T13:15:03-08:00' (YYYY-MM-DDTHH:mm:ssZ).
             */

            // https://code-maze.com/convert-datetime-to-iso-8601-string-csharp/
            return srcDateTime.ToString( "o" );
        }
    }
}
