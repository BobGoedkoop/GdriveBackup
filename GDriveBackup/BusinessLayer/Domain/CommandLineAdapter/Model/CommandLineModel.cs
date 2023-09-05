using System.Diagnostics.Eventing.Reader;
using Microsoft.SqlServer.Server;

namespace GDriveBackup.BusinessLayer.Domain.CommandLineAdapter.Model
{
    public class CommandLineModel
    {
        public bool BackupNew { get; set; } = false;
        public bool BackupAll { get; set; } = false;

        public bool ConfigReset { get; set; } = false;
        public bool ConfigResetLastRunDate { get; set; } = false;

        public bool Error { get; set; } = false;
    }
}
