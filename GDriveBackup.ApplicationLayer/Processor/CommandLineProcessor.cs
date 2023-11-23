using GDriveBackup.BusinessLayer.Domain.Backup;
using GDriveBackup.BusinessLayer.Domain.CommandLineAdapter.Model;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;

namespace GDriveBackup.ApplicationLayer.Processor
{
    public class CommandLineProcessor
    {
        private readonly ConsoleLogger _logger;


        public CommandLineProcessor()
        {
            this._logger = ConsoleLogger.GetInstance();
        }


        public void Process(CommandLineModel cmdLineModel)
        {
            if (cmdLineModel.ConfigReset)
            {
                Config.GetInstance().Reset();

                this._logger.Info($"Config file [{ApplicationSettings.GetInstance().ConfigPath}] has been reset.");
            }

            if (cmdLineModel.ConfigResetLastRunDate)
            {
                var config = Config.GetInstance();
                config.LastRunDate = Config.DefaultLastRunDate;
                config.Persist();

                this._logger.Info($"Config file [{ApplicationSettings.GetInstance().ConfigPath}] has had the LastRunDate reset.");
            }

            if (cmdLineModel.BackupAll)
            {
                var backup = new BackupDomain_v2( Config.DefaultLastRunDate );
                backup.Start();
            }
            else if (cmdLineModel.BackupChanges)
            {
                var backup = new BackupDomain_v2(Config.GetInstance().LastRunDate );
                backup.Start();
            }
        }
    }
}
