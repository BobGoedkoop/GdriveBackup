using System;
using System.IO;
using GDriveBackup.Crosscutting.Configuration;
using Newtonsoft.Json.Linq;

// ReSharper disable ArrangeThisQualifier
// ReSharper disable ArrangeAccessorOwnerBody

namespace GDriveBackup.DataLayer.LocalStore
{
    public class JsonStore
    {
        private readonly ApplicationSettings _appSettings;
        private JObject _db;

        #region Private section

        private static JObject DefaultDb()
        {
            return new JObject
            {
                { "LastRunDate", JsonStore.DefaultLastRunDate }
            };
        }

        private void Read()
        {
            try
            {
                this._db = JObject.Parse(
                    File.ReadAllText($"{this._appSettings.LocalStorePath}")
                );

            }
            catch ( FileNotFoundException )
            {
                File
                    .Create( $"{this._appSettings.LocalStorePath}" )
                    .Close();

                this._db = DefaultDb();
                this.Persist();

                this.Read();
            }
        }

        #endregion


        #region Singleton

        private static JsonStore _instance = null;

        protected JsonStore()
        {
            this._appSettings = ApplicationSettings.GetInstance();
            this.Read();
        }

        public static JsonStore GetInstance()
        {
            if ( _instance == null )
            {
                _instance = new JsonStore();
            }

            return _instance;
        }
        
        #endregion

        public static DateTime DefaultLastRunDate = DateTime.MinValue;

        public bool Persist()
        {
            File.WriteAllText( $"{this._appSettings.LocalStorePath}", this._db.ToString() );
            return true;
        }

        public bool Reset()
        {
            this._db = DefaultDb();
            this.Persist();

            this.Read();
            return true;
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
