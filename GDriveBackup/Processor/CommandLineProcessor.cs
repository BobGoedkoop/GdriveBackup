using GDriveBackup.BusinessLayer.Domain.CommandLine;
using GDriveBackup.Core.Constants;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;
using System;

namespace GDriveBackup.Processor
{
    public class CommandLineProcessor
    {
        public void Process(CommandLineParser cmdLine)
        {
            var logger = ConsoleLogger.GetInstance();
            logger.Log($"");
            logger.Log($">> Process commandline [{string.Join(ApplicationConstants.Space, cmdLine.Args)}].");

            if (cmdLine.HasCommand(CommandLineArgument.Config))
            {
                logger.Log($">> Processing command [{CommandLineArgument.Config}].");
                var command = cmdLine.GetCommand(CommandLineArgument.Config);

                if (command == CommandLineArgumentValue.Reset)
                {
                    logger.Log($"Processing command value [{CommandLineArgumentValue.Reset}].");

                    Config.GetInstance().Reset();

                    logger.Log($"Config file [{ApplicationConstants.ConfigPath}] has been reset.");
                }
                else
                {
                    logger.Log($"Unsupported command value.");

                }
                logger.Log($"<< Processing command [{CommandLineArgument.Config}].");
            }
            else
            {
                var backupProcessor = new BackupProcessor();
                backupProcessor.DoBackup();
            }
            logger.Log($"<< Process commandline [{string.Join(ApplicationConstants.Space, cmdLine.Args)}].");
        }
    }
}
