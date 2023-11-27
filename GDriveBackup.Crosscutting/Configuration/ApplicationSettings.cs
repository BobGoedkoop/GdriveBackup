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
            public const string ExportPath = "ExportPath";
            public const string JsonCredentialsPath = "JsonCredentialsPath";
            public const string LocalStorePath = "LocalStorePath";
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
    }

}
