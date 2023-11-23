using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDriveBackup.Crosscutting.Configuration.Exception
{
    public class ConfigurationException : System.Exception
    {
        public ConfigurationException(string keyOrMessage)
            : base(keyOrMessage)
        {
        }

        public ConfigurationException(string keyOrMessage, System.Exception innerException)
            : base(keyOrMessage, innerException)
        {
        }
    }
}
