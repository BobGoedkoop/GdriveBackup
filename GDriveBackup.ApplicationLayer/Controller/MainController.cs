using System;
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
            logger.Info($"{ApplicationConstants.ApplicationName} {ApplicationConstants.ApplicationVersion}");


            var cmdLineProcessor = new CommandLineProcessor();
            var cmdLineModel = CommandLineParser.Parse(args);
            cmdLineProcessor.Process(cmdLineModel);


            logger.Info($"\n\n{ApplicationConstants.ApplicationPressAnyKey}\n\n");
            Console.ReadKey();
        }
    }
}
