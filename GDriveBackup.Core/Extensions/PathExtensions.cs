using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDriveBackup.Core.Extensions
{
    public static class PathExtensions
    {
        public static string ToValidPath( this string path )
        {
            return path
                .Trim() // Strip leading and trailing whitespace
                .ReplaceInvalidPathCharacters();
        }

        public static string ReplaceInvalidPathCharacters( this string path, string replaceWith = "_" )
        {
            return string.Join( 
                replaceWith, 
                path.Split( Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries) 
            );
        }
    }
}
