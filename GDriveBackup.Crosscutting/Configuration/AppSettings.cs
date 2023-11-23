using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GDriveBackup.Crosscutting.Configuration.Exception;

namespace GDriveBackup.Crosscutting.Configuration
{
    public class AppSettings
    {
        protected AppSettings()
        {
        }


        protected static string GetAppSetting(string key, bool throwOnMissingSetting = true)
        {
            var settingValue = ConfigurationManager.AppSettings[key];

            if (string.IsNullOrEmpty(settingValue) || string.IsNullOrWhiteSpace(settingValue))
            {
                if (throwOnMissingSetting)
                {
                    throw new ConfigurationInvalidOrMissingValueException(key);
                }
            }

            return settingValue;
        }

    }
}
