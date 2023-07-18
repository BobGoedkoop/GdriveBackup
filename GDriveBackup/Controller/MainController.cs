using System;
using GDriveBackup.BusinessLayer.Domain.CommandLine;
using GDriveBackup.Core.Constants;
using GDriveBackup.Crosscutting.Logging;
using GDriveBackup.Processor;

namespace GDriveBackup.Controller
{
    public class MainController
    {
        public MainController() { }

        public void Execute(string[] args)
        {
            var logger = ConsoleLogger.GetInstance();
            logger.Log($"{ApplicationConstants.ApplicationName} {ApplicationConstants.ApplicationVersion}");

            var cmdLineProcessor = new CommandLineProcessor();
            var cmdLine = CommandLineParser.Parse(args);
            cmdLineProcessor.Process( cmdLine );

            if (cmdLine.HasCommand(CommandLineArgument.Backup))
            {
                var backupProcessor = new BackupProcessor();
                backupProcessor.DoBackup();
            }

            logger.Log($"\n\n{ApplicationConstants.ApplicationPressAnyKey}\n\n");
            Console.ReadKey();
        }
    }
}
