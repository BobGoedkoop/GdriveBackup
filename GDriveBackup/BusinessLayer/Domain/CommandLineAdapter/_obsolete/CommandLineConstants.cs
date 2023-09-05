using System;

namespace GDriveBackup.BusinessLayer.Domain.CommandLineAdapter._obsolete
{
    [Obsolete]
    public class CommandLineArgument
    {
        public const string Prefix = "-";

        public const string Config = "Config";
        public const string Backup = "Backup";
    }

    [Obsolete]
    public class CommandLineArgumentValue
    {
        public const string Reset = "Reset";
        public const string ResetLastRunDate = "ResetLastRunDate";

        public const string All = "All";
    }

}
