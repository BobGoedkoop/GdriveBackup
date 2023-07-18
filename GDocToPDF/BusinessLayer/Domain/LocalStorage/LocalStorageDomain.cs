using System;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.InteropServices;
using GDocToPDF.BusinessLayer.Types;
using Newtonsoft.Json.Linq;
// ReSharper disable ArrangeThisQualifier
// ReSharper disable ArrangeAccessorOwnerBody

namespace GDocToPDF.BusinessLayer.Domain.LocalStorage
{
    public class LocalStorageDomain
    {
        private JObject _db;

        private static JObject Empty()
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
                    File.ReadAllText($"{ApplicationConstants.LocalStoragePath}")
                );

            }
            catch ( FileNotFoundException )
            {
                File
                    .Create( $"{ApplicationConstants.LocalStoragePath}" )
                    .Close();

                this._db = Empty();
                this.Persist();

                this.Read();
            }
        }


        #region Singleton

        private static LocalStorageDomain _instance = null;

        protected LocalStorageDomain()
        {
            this.Read();
        }

        public static LocalStorageDomain GetInstance()
        {
            if ( _instance == null )
            {
                _instance = new LocalStorageDomain();
            }

            return _instance;
        }
        #endregion


        public bool Persist()
        {
            File.WriteAllText( $"{ApplicationConstants.LocalStoragePath}", this._db.ToString() );
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
