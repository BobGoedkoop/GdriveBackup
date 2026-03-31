using GDriveBackup.Crosscutting.Configuration.Exception;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GDriveBackup.Core.Extensions;

namespace GDriveBackup.Crosscutting.Configuration
{
    public class ApplicationSettings : AppSettings
    {
        // ReSharper disable once ClassNeverInstantiated.Local
        private class AppSettingsKey
        {
            //
            // Key (values)
            //
            public const string ApplicationName = "ApplicationName";
            public const string ApplicationVersion = "ApplicationVersion";
            public const string LogLevel = "LogLevel";
            public const string ExportPath = "ExportPath";
            public const string JsonCredentialsPath = "JsonCredentialsPath";
            public const string LocalStorePath = "LocalStorePath";
            public const string MaxConcurrentDownloads = "MaxConcurrentDownloads";
            public const string DriveApiMaxConcurrentListRequests = "DriveApiMaxConcurrentListRequests";
            public const string DriveApiRequestTimeoutSeconds = "DriveApiRequestTimeoutSeconds";
            public const string EnableConsoleHeartbeat = "EnableConsoleHeartbeat";
            public const string ConsoleHeartbeatIntervalSeconds = "ConsoleHeartbeatIntervalSeconds";
        }


        #region Singleton

        private static ApplicationSettings _instance;

        protected ApplicationSettings()
        {
            //this.ReadSettingsFile();
        }

        public static ApplicationSettings GetInstance()
        {
            if (_instance == null)
            {
                _instance = new ApplicationSettings();
            }
            return _instance;
        }

        #endregion


        /// <summary>
        /// </summary>
        public string ApplicationName
        {
            get
            {
                var value = GetAppSetting( AppSettingsKey.ApplicationName );
                value = value.Trim();
                return value;
            }
        }
        /// <summary>
        /// </summary>
        public string ApplicationVersion
        {
            get
            {
                var value = GetAppSetting(AppSettingsKey.ApplicationVersion);
                value = value.Trim();
                return value;
            }
        }
        /// <summary>
        /// Global minimum log level.
        /// Supported values: Trace, Debug, Info, Warn, Error, Fatal.
        /// </summary>
        public string LogLevel
        {
            get
            {
                var value = GetAppSetting(AppSettingsKey.LogLevel, false);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return "Info";
                }

                return value.Trim();
            }
        }
        /// <summary>
        /// </summary>
        public string ExportPath
        {
            get
            {
                var value = GetAppSetting(AppSettingsKey.ExportPath);
                value = value.Trim(); // Remove leading and trailing whitespace
                return value;
            }
        }
        /// <summary>
        /// </summary>
        public string JsonCredentialsPath
        {
            get
            {
                var value = GetAppSetting(AppSettingsKey.JsonCredentialsPath);
                value = value.Trim(); // Remove leading and trailing whitespace
                return value;
            }
        }

        /// <summary>
        /// </summary>
        public string LocalStorePath
        {
            get
            {
                var value = GetAppSetting(AppSettingsKey.LocalStorePath);
                value = value.Trim(); // Remove leading and trailing whitespace
                return value;
            }
        }

        /// <summary>
        /// Number of concurrent Google Drive export/download operations.
        /// Returns a safe default when missing/invalid to keep backups running.
        /// </summary>
        public int MaxConcurrentDownloads
        {
            get
            {
                const int fallbackValue = 6;

                var value = GetAppSetting(AppSettingsKey.MaxConcurrentDownloads, false);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return fallbackValue;
                }

                if (!int.TryParse(value.Trim(), out var parsedValue))
                {
                    return fallbackValue;
                }

                return parsedValue > 0 ? parsedValue : fallbackValue;
            }
        }

        /// <summary>
        /// Max concurrent Drive API list requests (folders/files metadata).
        /// Separate from download concurrency to avoid list-request bursts.
        /// </summary>
        public int DriveApiMaxConcurrentListRequests
        {
            get
            {
                const int fallbackValue = 4;
                var value = GetAppSetting(AppSettingsKey.DriveApiMaxConcurrentListRequests, false);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return fallbackValue;
                }

                if (!int.TryParse(value.Trim(), out var parsedValue))
                {
                    return fallbackValue;
                }

                return parsedValue > 0 ? parsedValue : fallbackValue;
            }
        }

        /// <summary>
        /// Http timeout for Drive requests (export/list). Useful for large exports.
        /// </summary>
        public int DriveApiRequestTimeoutSeconds
        {
            get
            {
                const int fallbackValue = 600;
                var value = GetAppSetting(AppSettingsKey.DriveApiRequestTimeoutSeconds, false);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return fallbackValue;
                }

                if (!int.TryParse(value.Trim(), out var parsedValue))
                {
                    return fallbackValue;
                }

                return parsedValue > 0 ? parsedValue : fallbackValue;
            }
        }

        /// <summary>
        /// Show a live progress heartbeat in console while backup is running.
        /// </summary>
        public bool EnableConsoleHeartbeat
        {
            get
            {
                var value = GetAppSetting(AppSettingsKey.EnableConsoleHeartbeat, false);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }

                return bool.TryParse(value.Trim(), out var parsedValue) && parsedValue;
            }
        }

        /// <summary>
        /// Interval (seconds) for console heartbeat refresh.
        /// </summary>
        public int ConsoleHeartbeatIntervalSeconds
        {
            get
            {
                const int fallbackValue = 3;
                var value = GetAppSetting(AppSettingsKey.ConsoleHeartbeatIntervalSeconds, false);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return fallbackValue;
                }

                if (!int.TryParse(value.Trim(), out var parsedValue))
                {
                    return fallbackValue;
                }

                return parsedValue > 0 ? parsedValue : fallbackValue;
            }
        }
    }

}
