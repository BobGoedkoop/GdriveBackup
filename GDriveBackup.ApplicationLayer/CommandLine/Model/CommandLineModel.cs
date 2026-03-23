namespace GDriveBackup.ApplicationLayer.CommandLine.Model
{
    public class CommandLineModel
    {
        public bool BackupChanges { get; set; } = false;
        public bool BackupAll { get; set; } = false;
        public bool BackupSplitOnly { get; set; } = false;
        public string SplitFileId { get; set; } = string.Empty;
        public string AutoSplitFileId { get; set; } = string.Empty;
        public bool HelpRequested { get; set; } = false;

        public bool ConfigReset { get; set; } = false;
        public bool ConfigResetLastRunDate { get; set; } = false;

        public bool Error { get; set; } = false;
    }
}
