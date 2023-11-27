using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;
using GDriveBackup.DataLayer.LocalStore;

namespace GDriveBackup.DataLayer.Repository
{
    public class RunDateRepository
    {
        private readonly IApplicationLogger _logger;

        public RunDateRepository()
        {
            this._logger = ApplicationLogger.GetInstance();
        }

        public DateTime DefaultLastRunDate => JsonStore.DefaultLastRunDate;

        public bool Reset()
        {
            this._logger.Trace($"RunDateRepository.Reset()");

            var result = this.SetLast( this.DefaultLastRunDate );
            return result;
        }

        public bool SetLast( DateTime lastRunDateTime )
        {
            this._logger.Trace( $"RunDateRepository.SetLast( [{lastRunDateTime}] )");

            var db = JsonStore.GetInstance();
            db.LastRunDate = lastRunDateTime;
            var result = db.Persist();

            return result;
        }

        public DateTime GetLast()
        {
            this._logger.Trace($"RunDateRepository.GetLast()");

            var db = JsonStore.GetInstance();
            return db.LastRunDate;
        }

    }
}
