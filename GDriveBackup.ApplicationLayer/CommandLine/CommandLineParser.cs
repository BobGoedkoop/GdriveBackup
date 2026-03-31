using System;
using System.Collections.Generic;
using System.Linq;
using CommandLine;
using GDriveBackup.ApplicationLayer.CommandLine.Model;

namespace GDriveBackup.ApplicationLayer.CommandLine
{
    public sealed class CommandLineParser
    {
        private static CommandLineModel _commandLineModel;

        private static void ParseOk(object opts)
        {
            var options = opts as CommandLineOptions;

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.Config == "reset")
            {
                _commandLineModel.ConfigReset = true;
            }

            if (options.Config == "resetLastRunDate")
            {
                _commandLineModel.ConfigResetLastRunDate = true;
            }

            if (options.Help)
            {
                _commandLineModel.HelpRequested = true;
            }

            if (options.Backup == "changes")
            {
                _commandLineModel.BackupChanges = true;
            }
            else if (options.Backup == "all")
            {
                _commandLineModel.BackupAll = true;
            }
            else if (!string.IsNullOrWhiteSpace(options.Backup))
            {
                _commandLineModel.Error = true;
            }
        }

        private static void ParseNok(IEnumerable<Error> errs)
        {
            _commandLineModel.Error = true;
        }

        public static CommandLineModel Parse(string[] args)
        {
            _commandLineModel = new CommandLineModel();

            // Treat missing or whitespace-only command line as "show help".
            var noEffectiveArgs = args == null || args.Length == 0 || args.All(string.IsNullOrWhiteSpace);
            if (noEffectiveArgs)
            {
                _commandLineModel.HelpRequested = true;
                return _commandLineModel;
            }

            foreach (var arg in args)
            {
                if (string.Equals(arg, "/h", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "/?", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "?", StringComparison.OrdinalIgnoreCase))
                {
                    _commandLineModel.HelpRequested = true;
                    return _commandLineModel;
                }
            }

            global::CommandLine.Parser.Default
                .ParseArguments<CommandLineOptions>(args)
                .WithParsed(ParseOk)
                .WithNotParsed(ParseNok);

            return _commandLineModel;
        }
    }
}
