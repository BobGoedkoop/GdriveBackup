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

        public void Log( Exception ex)
        {
            if (!Config.GetInstance().DebugConsole)
            {
                return;
            }

            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
        }

        public void Log(Google.Apis.Drive.v3.Data.File file)
        {
            if (!Config.GetInstance().DebugConsole)
            {
                return;
            }

            Console.WriteLine( file == null
                ? $"File: [null]."
                : $"File: Name [{file.Name}], MimeType [{file.MimeType}], Id [{file.Id}]." );
        }

        public void Log( IList<Google.Apis.Drive.v3.Data.File> files )
        {
            if (!Config.GetInstance().DebugConsole)
            {
                return;
            }

            foreach (var file in files)
            {
                this.Log( file );
            }
        }

    }
}
