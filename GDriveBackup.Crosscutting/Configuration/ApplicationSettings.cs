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
        private class AppSettingsConstants
        {
            //
            // Key (values)
            //
            public const string ApplicationName = "ApplicationName";
            public const string ApplicationVersion = "ApplicationVersion";
            public const string ExportPath = "ExportPath";
            public const string JsonCredentialsPath = "JsonCredentialsPath";
            public const string ConfigVersion = "ConfigVersion";
            public const string ConfigPath = "ConfigPath";
            //public const string SettingsFile = "SettingsFile";
        }



        #region Private Methods


        private void ReadSettingsFile()
        {

            //// Construct settings file based on the AppSettings
            //this.SettingsFile = GetAppSetting(AppSettingsConstants.SettingsFile);

            //var ext = Path.GetExtension(this.SettingsFile);
            //var fileName = Path.GetFileNameWithoutExtension(this.SettingsFile);
            //var path = Path.GetDirectoryName(this.SettingsFile);

            //if (path.IsEmptyString())
            //{
            //    throw new ConfigurationInvalidOrMissingValueException("AppSettings.SettingsFile");
            //}


            //#region Construct the filename 

            //var newFilename = fileName;

            //if (this.UseMachineName)
            //{
            //    newFilename = $"{newFilename}.{Environment.MachineName}";
            //}

            //newFilename = $"{newFilename}.{this.TargetEnvironment.ToString()}{ext}";

            //#endregion


            //this.SettingsFile = Path.Combine(
            //    // ReSharper disable once AssignNullToNotNullAttribute
            //    path,
            //    $"{newFilename}"
            //);

            //if (!File.Exists(this.SettingsFile))
            //{
            //    throw new ConfigurationInvalidOrMissingValueException($"AppSettings.SettingsFile [{this.SettingsFile}]");
            //}
        }

        #endregion Private Methods



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
                var value = GetAppSetting( AppSettingsConstants.ApplicationName );
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
                var value = GetAppSetting(AppSettingsConstants.ApplicationVersion);
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
                var value = GetAppSetting(AppSettingsConstants.ExportPath);
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
                var value = GetAppSetting(AppSettingsConstants.JsonCredentialsPath);
                value = value.Trim(); // Remove leading and trailing whitespace
                return value;
            }
        }

        /// <summary>
        /// </summary>
        public string ConfigVersion
        {
            get
            {
                var value = GetAppSetting(AppSettingsConstants.ConfigVersion);
                value = value.Trim(); // Remove leading and trailing whitespace
                return value;
            }
        }
        /// <summary>
        /// </summary>
        public string ConfigPath
        {
            get
            {
                var value = GetAppSetting(AppSettingsConstants.ConfigPath);
                value = value.Trim(); // Remove leading and trailing whitespace
                return value;
            }
        }
    }

}
