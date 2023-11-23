using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GDriveBackup.Core.Extensions
{
    public static class StringExtensions
    {
        public static string RemoveWhitespace(this string value)
        {
            return string.Join(
                "",
                value.Split(
                    default(string[]),
                    StringSplitOptions.RemoveEmptyEntries
                )
            );
        }

        public static string RemoveAllWhitespace(this string source)
        {
            return Regex.Replace(source, @"\s+", "");
        }

        /// <summary>
        /// </summary>
        /// <param name="line"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
        /// <remarks>See https://stackoverflow.com/questions/1977340/perform-trim-while-using-split</remarks>
        public static  string[] ToCommandLineArgs( this string line )
        {
            var parts = line.Split(
                    new char[] { /* one space */' ' }, 
                    StringSplitOptions.RemoveEmptyEntries
                    );


            return parts;
        }
    }
}
