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
        public static string ReplaceInvalidCharacters( this string path, string replaceWith = "_" )
        {
            return string.Join( 
                replaceWith, 
                path.Split( Path.GetInvalidFileNameChars() ) 
            );
        }
    }
}
