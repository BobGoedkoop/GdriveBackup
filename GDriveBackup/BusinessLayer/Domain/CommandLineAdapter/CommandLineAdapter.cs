using System.Collections.Generic;
using System.Security.Cryptography;
using CommandLine;

namespace GDriveBackup.BusinessLayer.Domain.CommandLineAdapter
{
    public class CommandLineOptions
    {
        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; set; }
    }

    public class CommandLineAdapter
    {
        private readonly string[] _args;

        #region Private section


        private static void ParseOk( object opts )
        {

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
                .ParseArguments<CommandLineOptions>( this._args )
                .WithParsed( ParseOk )
                .WithNotParsed( ParseNok )
                ;

        }
    }
}
