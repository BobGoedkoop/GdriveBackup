using GDriveBackup.BusinessLayer.Domain.CommandLineAdapter;
using GDriveBackup.Core.Constants;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;

namespace GDriveBackup.ApplicationLayer.Processor
{
    public class CommandLineProcessor
    {
        private readonly ConsoleLogger _logger;

        private void DoProcessCommandLine(CommandLineParser cmdLine)
        {
            if (cmdLine.HasCommand(CommandLineArgument.Config))
            {
                this._logger.Log($">> Processing command [{CommandLineArgument.Config}].");
                var command = cmdLine.GetCommand(CommandLineArgument.Config);

                if (command == CommandLineArgumentValue.Reset)
                {
                    this._logger.Log($"Processing command value [{CommandLineArgumentValue.Reset}].");

                    Config.GetInstance().Reset();

                    this._logger.Log($"Config file [{ApplicationConstants.ConfigPath}] has been reset.");
                }
                if (command == CommandLineArgumentValue.ResetLastRunDate)
                {
                    this._logger.Log($"Processing command value [{CommandLineArgumentValue.ResetLastRunDate}].");

                    var config = Config.GetInstance();
                    config.LastRunDate = Config.DefaultLastRunDate;
                    config.Persist();

                    this._logger.Log($"Config file [{ApplicationConstants.ConfigPath}] has had the LastRunDate reset.");
                }
                else
                {
                    this._logger.Log($"Unsupported command value [{string.Join(ApplicationConstants.Space, cmdLine.Args)}].");

                }
                this._logger.Log($"<< Processing command [{CommandLineArgument.Config}].");
            }

        }
        public CommandLineProcessor()
        {
            this._logger = ConsoleLogger.GetInstance();
        }


        public void Process(CommandLineParser cmdLine)
        {
            this._logger.Log($"\nProcess commandline [{string.Join(ApplicationConstants.Space, cmdLine.Args)}].");

            if (cmdLine.HasArguments())
            {
                this.DoProcessCommandLine( cmdLine );
            }
        }
    }
}
