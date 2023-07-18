using System;
using System.Collections.Generic;
using GDriveBackup.Crosscutting.Configuration;

namespace GDriveBackup.Crosscutting.Logging
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
            if ( !Config.GetInstance().DebugConsole )
            {
                return;
            }

            Console.WriteLine( text);
        }

        public void Log( IList<Google.Apis.Drive.v3.Data.File> files )
        {
            if (!Config.GetInstance().DebugConsole)
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
