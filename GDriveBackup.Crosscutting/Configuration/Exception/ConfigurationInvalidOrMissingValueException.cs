using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDriveBackup.Crosscutting.Configuration.Exception
{
    public class ConfigurationInvalidOrMissingValueException : ConfigurationException
    {
        private static string MakeInvalidOrMissingValueMessageFor(string key)
        {
            return $"Invalid or missing value for [{key}] in configuration file.";
        }
        public ConfigurationInvalidOrMissingValueException(string key)
            : base(MakeInvalidOrMissingValueMessageFor(key))
        {
        }
        public ConfigurationInvalidOrMissingValueException(string key, System.Exception innerException)
            : base(MakeInvalidOrMissingValueMessageFor(key), innerException)
        {
        }
    }
}
