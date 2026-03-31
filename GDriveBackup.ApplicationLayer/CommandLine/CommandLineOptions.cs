using CommandLine;

namespace GDriveBackup.ApplicationLayer.CommandLine
{
    public sealed class CommandLineOptions
    {
        [Option('b', "backup", Required = false, HelpText = "changes: only changes since last run, all: all files.")]
        public string Backup { get; set; }

        [Option('c', "config", Required = false, HelpText = "Manipulate the configuration.")]
        public string Config { get; set; }

        [Option('h', "help", Required = false, HelpText = "Show command line help.")]
        public bool Help { get; set; }
    }
}
