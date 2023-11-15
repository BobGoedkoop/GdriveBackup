using System;
using System.Collections.Generic;
using GDriveBackup.Crosscutting.Configuration;

namespace GDriveBackup.Crosscutting.Logging
{
    public class ConsoleLogger
    {

        #region Private section 

        private void Log(string text)
        {
            if (!Config.GetInstance().DebugConsole)
            {
                return;
            }

            Console.WriteLine($"{DateTime.Now.ToLongTimeString()} {text}");
        }

        private void Log(Exception ex)
        {
            if (!Config.GetInstance().DebugConsole)
            {
                return;
            }

            Console.WriteLine($"{DateTime.Now.ToLongTimeString()} {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        private void Log(Google.Apis.Drive.v3.Data.File file)
        {
            if (!Config.GetInstance().DebugConsole)
            {
                return;
            }

            Console.WriteLine(file == null
                ? $"{DateTime.Now.ToLongTimeString()} File: [null]."
                : $"{DateTime.Now.ToLongTimeString()} File: Name [{file.Name}], MimeType [{file.MimeType}], Id [{file.Id}].");
        }

        private void Log(IList<Google.Apis.Drive.v3.Data.File> files)
        {
            if (!Config.GetInstance().DebugConsole)
            {
                return;
            }

            foreach (var file in files)
            {
                this.Log(file);
            }
        }

        #endregion


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


        #region Debug

        private const string TxtDebug = "Debug";

        public void Debug( string message )
        {
            this.Log( $"{TxtDebug} {message}");
        }

        public void Debug(string message, Exception ex)
        {
            this.Log($"{TxtDebug} {message}");
            this.Log( ex );
        }

        public void Debug(Google.Apis.Drive.v3.Data.File file)
        {
            this.Log( file );
        }
        public void Debug(IList<Google.Apis.Drive.v3.Data.File> files)
        {
            this.Log( files );
        }


        #endregion


        #region Info

        private const string TxtInfo = "Info";

        public void Info(string message)
        {
            this.Log($"{TxtInfo} {message}");
        }

        public void Info(string message, Exception ex)
        {
            this.Log($"{TxtInfo} {message}");
            this.Log(ex);
        }

        #endregion


        #region Warn

        private const string TxtWarn = "Warn";

        public void Warn(string message)
        {
            this.Log($"{TxtWarn} {message}");
        }

        public void Warn(string message, Exception ex)
        {
            this.Log($"{TxtWarn} {message}");
            this.Log(ex);
        }

        #endregion
        
        
        #region Error

        private const string TxtError = "Error";

        public void Error(string message)
        {
            this.Log($"{TxtError} {message}");
        }

        public void Error(string message, Exception ex)
        {
            this.Log($"{TxtError} {message}");
            this.Log(ex);
        }
        #endregion
    }
}
