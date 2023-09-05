using System.Collections.Generic;
using System.Security.Cryptography;
using CommandLine;

namespace GDriveBackup.BusinessLayer.Domain.CommandLineAdapter
{
    public class CommandLineOptions
    {
        [Option('b', "backup", Required = true, HelpText = "Do the backup.")]
        public bool Backup { get; set; }

        [Option('c', "config", Required = false, HelpText = "Manipulate the configuration.")]
        public bool Config { get; set; }
    }

    [Verb( "all", HelpText ="Do the full backup, ignore last run date.")]
    public class CommandLineBackupOptions
    {
        [Option('a', "all", Required = false, HelpText = "Full backup.")]
        public bool All { get; set; }
    }

    public class CommandLineAdapter
    {
        private readonly string[] _args;

        #region Private section


        private static void ParseOk( object opts )
        {
            var options = opts as CommandLineOptions;

        }

        private static void ParseNok(IEnumerable<Error> errs)
        {
        }

        #endregion


        public CommandLineAdapter()
            : this( new string[] { } )
        {

        }

        public CommandLineAdapter( string[] args )
        {
            this._args = args;
        }

        public void Parse()
        {
            CommandLine.Parser.Default
                .ParseArguments<
                    CommandLineOptions,
                    CommandLineBackupOptions>( this._args )
                .WithParsed( ParseOk )
                .WithNotParsed( ParseNok )
                ;

        }
    }
}
