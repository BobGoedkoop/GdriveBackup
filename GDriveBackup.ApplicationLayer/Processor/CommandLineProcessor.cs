using GDriveBackup.BusinessLayer.Domain.Backup;
using GDriveBackup.ApplicationLayer.CommandLine.Model;
using GDriveBackup.BusinessLayer.Domain.Run;
using GDriveBackup.Crosscutting.Logging;
using System;

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
            if (cmdLineModel.HelpRequested)
            {
                this.ShowHelp();
                return;
            }

            var runDateDomain = new RunDateDomain();

            if (cmdLineModel.Error)
            {
                this._logger.Error("Invalid command line arguments. Use: --backup changes|all.");
                return;
            }

            if (cmdLineModel.ConfigResetLastRunDate)
            {
                runDateDomain.Reset();
            }

            if (cmdLineModel.BackupAll)
            {
                var backup = new BackupDomain(runDateDomain.DefaultLastRunDate);
                backup.Start();
            }
            else if (cmdLineModel.BackupChanges)
            {
                var backup = new BackupDomain(runDateDomain.LastRunDate);
                backup.Start();
            }
        }

        private void ShowHelp()
        {
            Console.WriteLine("GDriveBackup command line options:");
            Console.WriteLine("  -b, --backup <changes|all>");
            Console.WriteLine("      changes  Backup only files changed since LastRunDate.");
            Console.WriteLine("      all      Backup all files.");
            Console.WriteLine();
            Console.WriteLine("  -c, --config <reset|resetLastRunDate>");
            Console.WriteLine("      reset             Reset config state.");
            Console.WriteLine("      resetLastRunDate  Reset LastRunDate checkpoint.");
            Console.WriteLine();
            Console.WriteLine("  -h, --help, /h, /?");
            Console.WriteLine("      Show this help.");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  GDriveBackup.exe --backup changes");
            Console.WriteLine("  GDriveBackup.exe --backup all");
        }
    }
}
