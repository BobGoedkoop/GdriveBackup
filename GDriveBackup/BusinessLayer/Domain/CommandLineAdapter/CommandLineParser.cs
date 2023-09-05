using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CommandLine;
using GDriveBackup.BusinessLayer.Domain.CommandLineAdapter.Model;

namespace GDriveBackup.BusinessLayer.Domain.CommandLineAdapter
{

    public class CommandLineParser
    {
        private readonly string[] _args;

        private readonly CommandLineModel _commandLineModel;

        #region Private section

        /// <summary>
        /// Command line arguments passed are valid
        /// </summary>
        /// <param name="opts"></param>
        private void ParseOk( object opts )
        {
            var options = opts as CommandLineOptions;

            if ( options == null )
            {
                throw new ArgumentNullException( "options" );
            }
            
            // Todo Set the _commandLineModel

            if ( options.Backup == "new" )
            {
                this._commandLineModel.BackupNew = true;
            }
            if (options.Backup == "all")
            {
                this._commandLineModel.BackupAll = true;
            }
        }

        /// <summary>
        /// Command line arguments passed are NOT valid
        /// </summary>
        /// <param name="errs"></param>
        private void ParseNok(IEnumerable<Error> errs)
        {
            this._commandLineModel.Error = true;
        }

        #endregion


        public CommandLineParser()
            : this( new string[] { } )
        {

        }

        public CommandLineParser( string[] args )
        {
            this._commandLineModel = new CommandLineModel();
            this._args = args;
        }

        public CommandLineModel Parse()
        {
            CommandLine.Parser.Default
                .ParseArguments<CommandLineOptions>( this._args )
                .WithParsed( ParseOk )
                .WithNotParsed( ParseNok )
                ;
            return this._commandLineModel;
        }
    }
}
