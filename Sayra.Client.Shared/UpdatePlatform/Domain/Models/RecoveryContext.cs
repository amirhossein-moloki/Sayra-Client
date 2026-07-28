using System.Collections.Generic;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Details the system context, critical files, hashes, and services required to perform recovery operations.
    /// </summary>
    public class RecoveryContext
    {
        public string TargetVersion { get; set; } = string.Empty;
        public string SourceVersion { get; set; } = string.Empty;
        public string InstallationDirectory { get; set; } = string.Empty;
        public string SnapshotDirectory { get; set; } = string.Empty;
        public List<string> CriticalFiles { get; set; } = new();
        public Dictionary<string, string> FileHashes { get; set; } = new();
        public string ConfigurationFilePath { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
    }
}
