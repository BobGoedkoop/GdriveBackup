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
            logger.Log($"{ApplicationConstants.ApplicationName} {ApplicationConstants.ApplicationVersion}");

            var cmdLineModel = new CommandLineParser(args)
                .Parse();

            var cmdLineProcessor = new CommandLineProcessor();
            cmdLineProcessor.Process(cmdLineModel);


            logger.Log($"\n\n{ApplicationConstants.ApplicationPressAnyKey}\n\n");
            Console.ReadKey();
        }
    }
}
