using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Stores comprehensive update operation history fields.
    /// </summary>
    public class UpdateHistoryRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PackageId { get; set; }
        public string Version { get; set; } = string.Empty;
        public string PreviousVersion { get; set; } = string.Empty;
        public DateTime InstallationTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        public string Status { get; set; } = string.Empty; // 'STAGED', 'COMPLETED', 'FAILED', 'ROLLED_BACK'
        public TimeSpan Duration { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string DeviceIdentifier { get; set; } = string.Empty;
        public bool TelemetryUploaded { get; set; }
    }
}
