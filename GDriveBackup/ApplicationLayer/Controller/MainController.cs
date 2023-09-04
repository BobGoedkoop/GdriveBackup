using System;
using CommandLine;
using GDriveBackup.ApplicationLayer.Processor;
using GDriveBackup.BusinessLayer.Domain.CommandLineAdapter;
using GDriveBackup.Core.Constants;
using GDriveBackup.Crosscutting.Logging;

namespace GDriveBackup.ApplicationLayer.Controller
{
    public class MainController
    {
        public MainController() { }

        public void Execute(string[] args)
        {
            var logger = ConsoleLogger.GetInstance();
            logger.Log($"{ApplicationConstants.ApplicationName} {ApplicationConstants.ApplicationVersion}");


            var cmdLine2 = new CommandLineAdapter( args );
            cmdLine2.Parse();

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
