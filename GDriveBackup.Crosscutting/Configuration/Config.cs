using System;
using System.IO;
using System.Runtime.CompilerServices;
using GDriveBackup.Core.Constants;
using Newtonsoft.Json.Linq;

// ReSharper disable ArrangeThisQualifier
// ReSharper disable ArrangeAccessorOwnerBody

namespace GDriveBackup.Crosscutting.Configuration
{
    public class Config
    {
        private readonly ApplicationSettings _settings;
        private JObject _db;

        private JObject Default()
        {
            return new JObject
            {
                { "Version", this._settings.ConfigVersion },

                {
                    "Application", new JObject
                    {
                        { "Name" , $"{this._settings.ApplicationName}" },
                        { "Version" , $"{this._settings.ApplicationVersion}" },

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
                    File.ReadAllText($"{this._settings.ConfigPath}")
                );

            }
            catch ( FileNotFoundException )
            {
                File
                    .Create( $"{this._settings.ConfigPath}" )
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
            this._settings = ApplicationSettings.GetInstance();
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
            File.WriteAllText( $"{this._settings.ConfigPath}", this._db.ToString() );
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
