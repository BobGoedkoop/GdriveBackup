using System;
using System.Collections.Generic;
using System.IO;
using GDriveBackup.Crosscutting.Configuration;

namespace GDriveBackup.Crosscutting.Logging
{
    public interface IApplicationLogger
    {
        void ShutDown();
        
        void Trace( string message );
        void Trace( string message, Exception ex );
        
        void Debug( string message );
        void Debug( string message, Exception ex );
        void Debug( Google.Apis.Drive.v3.Data.File file );
        void Debug( IList<Google.Apis.Drive.v3.Data.File> files );
        
        void Info( string message );
        void Info( string message, Exception ex );
        
        void Warn( string message );
        void Warn( string message, Exception ex );
        
        void Error( string message );
        void Error( string message, Exception ex );

        void Fatal( string message );
        void Fatal( string message, Exception ex );
    }

    public class ApplicationLogger: IApplicationLogger, IDisposable
    {
        /// <summary>
        /// </summary>
        /// <see cref="https://github.com/NLog/NLog/wiki/Tutorial"/>
        //private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static readonly NLog.Logger Logger = NLog
            .LogManager
            .GetLogger(
                // This gets the executable filename without extension.
                System.Diagnostics.Process.GetCurrentProcess().ProcessName 
            );



        #region Singleton

        private static IApplicationLogger _instance;

        protected ApplicationLogger()
        {

        }


        public static IApplicationLogger GetInstance()
        {
            if ( _instance == null )
            {
                _instance = new ApplicationLogger();
            }

            return _instance;
        }

        #endregion


        #region IDispose

        private void ReleaseUnmanagedResources()
        {
            this.ShutDown();
        }

        ~ApplicationLogger()
        {
            ReleaseUnmanagedResources();
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        #endregion


        #region IApplicationLogger

        public void ShutDown()
        {
            NLog.LogManager.Shutdown(); // Flush and close down internal threads and timers
        }


        #region Trace

        public void Trace(string message)
        {
            Logger.Trace(message);
        }

        public void Trace(string message, Exception ex)
        {
            Logger.Trace(message, ex);
        }

        #endregion

        #region Debug

        public void Debug(string message)
        {
            Logger.Debug(message);
        }

        public void Debug(string message, Exception ex)
        {
            Logger.Debug(message, ex);
        }

        public void Debug(Google.Apis.Drive.v3.Data.File file)
        {
            this.Debug(file == null
                ? $"{DateTime.Now.ToLongTimeString()} File: [null]."
                : $"{DateTime.Now.ToLongTimeString()} File: Name [{file.Name}], MimeType [{file.MimeType}], Id [{file.Id}].");
        }

        public void Debug(IList<Google.Apis.Drive.v3.Data.File> files)
        {
            foreach (var file in files)
            {
                this.Debug(file);
            }
        }


        #endregion

        #region Info

        public void Info(string message)
        {
            Logger.Info(message);
        }

        public void Info(string message, Exception ex)
        {
            Logger.Info(message, ex);
        }

        #endregion

        #region Warn

        public void Warn(string message)
        {
            Logger.Warn(message);
        }

        public void Warn(string message, Exception ex)
        {
            Logger.Warn(message, ex);
        }

        #endregion

        #region Error

        public void Error(string message)
        {
            Logger.Error(message);
        }

        public void Error(string message, Exception ex)
        {
            Logger.Error(message, ex);
        }

        #endregion

        #region Fatal

        public void Fatal(string message)
        {
            Logger.Fatal(message);
        }

        public void Fatal(string message, Exception ex)
        {
            Logger.Fatal(message, ex);
        }

        #endregion

        #endregion
    }
}
