using System.Collections.Generic;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Holds the results of post-installation health checks.
    /// </summary>
    public class HealthValidationResult
    {
        public bool IsHealthy { get; set; }
        public List<string> ErrorMessages { get; set; } = new();
        public bool ApplicationStarted { get; set; }
        public bool ServicesRunning { get; set; }
        public bool CriticalFilesExist { get; set; }
        public bool FileHashesValid { get; set; }
        public bool ConfigurationReadable { get; set; }
    }
}
