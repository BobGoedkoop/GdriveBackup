using System;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;
using GDriveBackup.DataLayer.Repository;
using Google.Apis.Logging;

namespace GDriveBackup.BusinessLayer.Domain.Run
{
    public  class RunDateDomain
    {
        private readonly RunDateRepository _runDateRepository;
        private readonly IApplicationLogger _logger;

        public RunDateDomain()
        {
            this._runDateRepository = new RunDateRepository();
            this._logger = ApplicationLogger.GetInstance();
        }

        public DateTime DefaultLastRunDate => this._runDateRepository.DefaultLastRunDate;

        public DateTime LastRunDate
        {
            get => this._runDateRepository.GetLast();
            set => this._runDateRepository.SetLast( value );
        }

        public bool Reset()
        {
            var result = this._runDateRepository.Reset();
            return result;

        }
    }
}
