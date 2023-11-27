using GDriveBackup.BusinessLayer.Domain.Backup;
using GDriveBackup.BusinessLayer.Domain.CommandLineAdapter.Model;
using GDriveBackup.BusinessLayer.Domain.Run;
using GDriveBackup.Crosscutting.Logging;

namespace GDriveBackup.ApplicationLayer.Processor
{
    public class CommandLineProcessor
    {
        private readonly IApplicationLogger _logger;


        public CommandLineProcessor()
        {
            this._logger = ApplicationLogger.GetInstance();
        }


        public void Process(CommandLineModel cmdLineModel)
        {
            var runDateDomain = new RunDateDomain();

            if (cmdLineModel.ConfigResetLastRunDate)
            {
                runDateDomain.Reset();
            }

            if (cmdLineModel.BackupAll)
            {
                var backup = new BackupDomain(runDateDomain.DefaultLastRunDate );
                backup.Start();
            }
            else if (cmdLineModel.BackupChanges)
            {
                var backup = new BackupDomain(runDateDomain.LastRunDate );
                backup.Start();
            }
        }
    }
}
