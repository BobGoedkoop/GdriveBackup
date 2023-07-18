﻿using System;
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
                {
                    "Application", new JObject
                    {
                        { "Name" , $"{ApplicationConstants.ApplicationName}" },
                        { "Version" , $"{ApplicationConstants.ApplicationVersion}" },

                    }
                },
                
                { "LastRunDateTime", DateTime.MinValue },

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

        public DateTime LastRunDateTime
        {
            get
            {
                var oLastRunDateTime  = this._db["LastRunDateTime"];
                if ( oLastRunDateTime == null )
                {
                    oLastRunDateTime = DateTime.MinValue;
                }
                return DateTime.Parse(oLastRunDateTime.ToString() );
            }
            set
            {
                this._db["LastRunDateTime"] = value;
            }
        }
    }
}