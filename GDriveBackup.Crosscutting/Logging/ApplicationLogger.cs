using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GDriveBackup.Crosscutting.Configuration;

namespace GDriveBackup.Crosscutting.Logging
{
    public interface IApplicationLogger
    {
        void ShutDown();
        void SetContext(string key, string value);
        void ClearContext(string key);
        
        void Trace( string message );
        void Trace( string message, Exception ex );
        
        void Debug( string message );
        void Debug( string message, Exception ex );
        void Debug(string message, IDictionary<string, object> properties);
        void Debug( Google.Apis.Drive.v3.Data.File file );
        void Debug( IList<Google.Apis.Drive.v3.Data.File> files );
        
        void Info( string message );
        void Info( string message, Exception ex );
        void Info(string message, IDictionary<string, object> properties);
        
        void Warn( string message );
        void Warn( string message, Exception ex );
        void Warn(string message, IDictionary<string, object> properties);
        
        void Error( string message );
        void Error( string message, Exception ex );
        void Error(string message, IDictionary<string, object> properties);

        void Fatal( string message );
        void Fatal( string message, Exception ex );
    }

    public class ApplicationLogger: IApplicationLogger, IDisposable
    {
        private static readonly ConcurrentDictionary<string, IDisposable> ScopeProperties =
            new ConcurrentDictionary<string, IDisposable>(StringComparer.Ordinal);
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
            var configuredLevel = ApplicationSettings.GetInstance().LogLevel;
            if (!string.IsNullOrWhiteSpace(configuredLevel))
            {
                try
                {
                    NLog.LogManager.GlobalThreshold = NLog.LogLevel.FromString(configuredLevel);
                }
                catch
                {
                    NLog.LogManager.GlobalThreshold = NLog.LogLevel.Info;
                }
            }
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

        private static NLog.LogEventInfo BuildEvent(
            NLog.LogLevel level,
            string message,
            Exception ex,
            IDictionary<string, object> properties)
        {
            var logEvent = new NLog.LogEventInfo(level, Logger.Name, message)
            {
                Exception = ex
            };

            if (properties != null)
            {
                foreach (var property in properties)
                {
                    if (!string.IsNullOrWhiteSpace(property.Key))
                    {
                        logEvent.Properties[property.Key] = property.Value ?? string.Empty;
                    }
                }
            }

            return logEvent;
        }

        public void ShutDown()
        {
            NLog.LogManager.Shutdown(); // Flush and close down internal threads and timers
        }

        public void SetContext(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            this.ClearContext(key);
            var scope = NLog.ScopeContext.PushProperty(key, value ?? string.Empty);
            ScopeProperties[key] = scope;
        }

        public void ClearContext(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (ScopeProperties.TryRemove(key, out var scope))
            {
                scope.Dispose();
            }
        }


        #region Trace

        public void Trace(string message)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Trace(message);
        }

        public void Trace(string message, Exception ex)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Trace(ex, message);
        }

        #endregion

        #region Debug

        public void Debug(string message)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Debug(message);
        }

        public void Debug(string message, Exception ex)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Debug(ex, message);
        }

        public void Debug(string message, IDictionary<string, object> properties)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Log(BuildEvent(NLog.LogLevel.Debug, message, null, properties));
        }

        public void Debug(Google.Apis.Drive.v3.Data.File file)
        {
            this.Debug(file == null
                ? "File: [null]."
                : $"File: Name [{file.Name}], MimeType [{file.MimeType}], Id [{file.Id}].");
        }

        public void Debug(IList<Google.Apis.Drive.v3.Data.File> files)
        {
            if (files == null)
            {
                this.Debug("File list: [null].");
                return;
            }

            // Keep debug output actionable: list size + a few examples, not entire payload dumps.
            this.Debug($"File list count [{files.Count}].");
            var previewCount = Math.Min(5, files.Count);
            for (var i = 0; i < previewCount; i++)
            {
                this.Debug(files[i]);
            }

            if (files.Count > previewCount)
            {
                this.Debug($"File list preview truncated. Remaining [{files.Count - previewCount}] items not logged.");
            }
        }


        #endregion

        #region Info

        public void Info(string message)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Info(message);
        }

        public void Info(string message, Exception ex)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Info(ex, message);
        }

        public void Info(string message, IDictionary<string, object> properties)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Log(BuildEvent(NLog.LogLevel.Info, message, null, properties));
        }

        #endregion

        #region Warn

        public void Warn(string message)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Warn(message);
        }

        public void Warn(string message, Exception ex)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Warn(ex, message);
        }

        public void Warn(string message, IDictionary<string, object> properties)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Log(BuildEvent(NLog.LogLevel.Warn, message, null, properties));
        }

        #endregion

        #region Error

        public void Error(string message)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Error(message);
        }

        public void Error(string message, Exception ex)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Error(ex, message);
        }

        public void Error(string message, IDictionary<string, object> properties)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Log(BuildEvent(NLog.LogLevel.Error, message, null, properties));
        }

        #endregion

        #region Fatal

        public void Fatal(string message)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Fatal(message);
        }

        public void Fatal(string message, Exception ex)
        {
            ConsoleOutputCoordinator.PrepareForLogLine();
            Logger.Fatal(ex, message);
        }

        #endregion

        #endregion
    }
}



