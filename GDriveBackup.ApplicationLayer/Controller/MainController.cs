using System;
using GDriveBackup.ApplicationLayer.Processor;
using GDriveBackup.BusinessLayer.Domain.CommandLineAdapter;
using GDriveBackup.Core.Constants;
using GDriveBackup.Crosscutting.Configuration;
using GDriveBackup.Crosscutting.Logging;

namespace GDriveBackup.ApplicationLayer.Controller
{
    public class MainController
    {
        public MainController() { }

        public void Execute(string[] args)
        {
            var settings = ApplicationSettings.GetInstance();

            var logger = ApplicationLogger.GetInstance();
            logger.Info($"\n\n\n\n\n========== ========= ========== ==========");
            logger.Info($"{settings.ApplicationName} {settings.ApplicationVersion}");


            var cmdLineProcessor = new CommandLineProcessor();
            var cmdLineModel = CommandLineParser.Parse(args);
            cmdLineProcessor.Process(cmdLineModel);


            Console.WriteLine($"{ApplicationConstants.ApplicationPressAnyKey}");
            Console.ReadKey();
        }
    }
}
