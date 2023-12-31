﻿using CommandLine.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDriveBackup.BusinessLayer.Domain.CommandLineAdapter
{
    public sealed class CommandLineOptions
    {
        [Option('b', "backup", Required = false,  HelpText = "changes: only changes since last run, all: all files.")]
        public string Backup { get; set; }

        [Option('c', "config", Required = false, HelpText = "Manipulate the configuration.")]
        public string Config { get; set; }
    }
}
