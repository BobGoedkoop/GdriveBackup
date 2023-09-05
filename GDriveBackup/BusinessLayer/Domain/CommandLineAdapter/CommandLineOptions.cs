using CommandLine.Text;
using CommandLine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDriveBackup.BusinessLayer.Domain.CommandLineAdapter
{
    public sealed class CommandLineOptions
    {
        [Option('b', "backup", Required = true, Default = "new", HelpText = "new: only changes since last run, all: all files.")]
        public string Backup { get; set; }

        [Option('c', "config", Required = false, HelpText = "Manipulate the configuration.")]
        public string Config { get; set; }
    }
}
