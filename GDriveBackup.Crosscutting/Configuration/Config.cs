using System;
using System.IO;
using GDriveBackup.Core.Constants;
using Newtonsoft.Json.Linq;

// ReSharper disable ArrangeThisQualifier
// ReSharper disable ArrangeAccessorOwnerBody

namespace GDriveBackup.Crosscutting.Configuration
{
    public class Config
    {
        private JObject _db;

        private static JObject Default()
        {
            return new JObject
            {
                { "Version", ApplicationConstants.ConfigVersion },

                {
                    "Application", new JObject
                    {
                        { "Name" , $"{ApplicationConstants.ApplicationName}" },
                        { "Version" , $"{ApplicationConstants.ApplicationVersion}" },

                    }
                },
                
                { "LastRunDate", Config.DefaultLastRunDate },

                {
                    "Debug", new JObject
                    {
                        { "Console", true }
                    }
                }
            };
        }

        private void Read()
        {
            try
            {
                this._db = JObject.Parse(
                    File.ReadAllText($"{ApplicationConstants.ConfigPath}")
                );

            }
            catch ( FileNotFoundException )
            {
                File
                    .Create( $"{ApplicationConstants.ConfigPath}" )
                    .Close();

                this._db = Default();
                this.Persist();

                this.Read();
            }
        }


        #region Singleton

        private static Config _instance = null;

        protected Config()
        {
            this.Read();
        }

        public static Config GetInstance()
        {
            if ( _instance == null )
            {
                _instance = new Config();
            }

            return _instance;
        }
        
        #endregion

        public static DateTime DefaultLastRunDate = DateTime.MinValue;

        public bool Persist()
        {
            File.WriteAllText( $"{ApplicationConstants.ConfigPath}", this._db.ToString() );
            return true;
        }

        public bool Reset()
        {
            this._db = Default();
            this.Persist();

            this.Read();
            return true;
        }

        public bool DebugConsole
        {
            get
            {
                var oDebug = (JObject)this._db["Debug"];
                if ( oDebug == null )
                {
                    return false;
                }

                var oDebugConsoleValue = oDebug["Console"];
                if (oDebugConsoleValue == null)
                {
                    oDebugConsoleValue = false;
                }

                return bool.Parse(oDebugConsoleValue.ToString() );
            }
            set
            {
                this._db["ConsoleDebug"] = value;
            }
        }

        public DateTime LastRunDate
        {
            get
            {
                var oLastRunDate  = this._db["LastRunDate"];
                if ( oLastRunDate == null )
                {
                    oLastRunDate = DateTime.MinValue;
                }
                return DateTime.Parse(oLastRunDate.ToString() );
            }
            set
            {
                this._db["LastRunDate"] = value;
            }
        }
    }
}
