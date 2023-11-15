using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDriveBackup.Core.Extensions
{
    public static class StringExtensions
    {
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
