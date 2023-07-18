using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GDocToPDF.BusinessLayer.Domain.LocalStorage;

namespace GDocToPDF.BusinessLayer.Domain.Logging
{
    public class ConsoleLogger
    {

        #region Singleton

        private static ConsoleLogger _instance;

        protected ConsoleLogger()
        {

        }

        public static ConsoleLogger GetInstance()
        {
            if ( _instance == null )
            {
                _instance = new ConsoleLogger();
            }

            return _instance;
        }

        #endregion

        public void Log( string text )
        {
            if ( !LocalStorageDomain.GetInstance().DebugConsole )
            {
                return;
            }

            Console.WriteLine( text);
        }

        public void Log( IList<Google.Apis.Drive.v3.Data.File> files )
        {
            if (!LocalStorageDomain.GetInstance().DebugConsole)
            {
                return;
            }

            foreach (var driveFile in files)
            {
                Console.WriteLine($"{driveFile.Name}  {driveFile.MimeType}  {driveFile.Id}");
            }
        }
    }
}
