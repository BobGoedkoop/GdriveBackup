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
                this._logger.Error("Invalid command line arguments. Use: --backup changes|all [--auto-split-file-id <GoogleDriveFileId>].");
                return;
            }

            if (cmdLineModel.ConfigResetLastRunDate)
            {
                runDateDomain.Reset();
            }

            if (cmdLineModel.BackupAll)
            {
                var backup = new BackupDomain(runDateDomain.DefaultLastRunDate, cmdLineModel.AutoSplitFileId);
                backup.Start();
            }
            else if (cmdLineModel.BackupChanges)
            {
                var backup = new BackupDomain(runDateDomain.LastRunDate, cmdLineModel.AutoSplitFileId);
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
            Console.WriteLine("  --auto-split-file-id <GoogleDriveFileId>");
            Console.WriteLine("      Optional filter for oversized-doc fallback during --backup changes|all.");
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
            Console.WriteLine("  GDriveBackup.exe --backup changes --auto-split-file-id 1xysnoNlOmRI-rbnuvAxzxs8ho4AghurRDPm22YtxlI8");
            Console.WriteLine("  GDriveBackup.exe --backup all");
        }
    }
}
