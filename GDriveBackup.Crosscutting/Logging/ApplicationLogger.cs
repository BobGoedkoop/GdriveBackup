using System;

namespace GDriveBackup.Crosscutting.Logging
{
    public class ApplicationLogger
    {
        /// <summary>
        /// </summary>
        /// <see cref="https://github.com/NLog/NLog/wiki/Tutorial"/>
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        #region Private section 


        #endregion


        #region Singleton

        private static ApplicationLogger _instance;

        protected ApplicationLogger()
        {

        }

        public static ApplicationLogger GetInstance()
        {
            if ( _instance == null )
            {
                _instance = new ApplicationLogger();
            }

            return _instance;
        }

        #endregion


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

        public void Debug( string message )
        {
            Logger.Debug( message );
        }

        public void Debug(string message, Exception ex)
        {
            Logger.Debug( message, ex );
        }

        //public void Debug(Google.Apis.Drive.v3.Data.File file)
        //{
        //    this.Log( file );
        //}
        //public void Debug(IList<Google.Apis.Drive.v3.Data.File> files)
        //{
        //    this.Log( files );
        //}

        #endregion


        #region Info

        public void Info(string message)
        {
            Logger.Info( message );
        }

        public void Info(string message, Exception ex)
        {
            Logger.Info( message, ex );
        }

        #endregion


        #region Warn

        public void Warn(string message)
        {
            Logger.Warn( message );
        }

        public void Warn(string message, Exception ex)
        {
            Logger.Warn( message, ex );
        }

        #endregion
        
        
        #region Error

        public void Error(string message)
        {
            Logger.Error( message );
        }

        public void Error(string message, Exception ex)
        {
            Logger.Error( message, ex );
        }

        #endregion
    }
}
