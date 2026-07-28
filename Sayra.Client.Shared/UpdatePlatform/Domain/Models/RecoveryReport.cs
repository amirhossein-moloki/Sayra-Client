using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the comprehensive report output from an automated recovery or self-healing procedure.
    /// </summary>
    public class RecoveryReport
    {
        public Guid RecoveryId { get; set; } = Guid.NewGuid();
        public bool Succeeded { get; set; }
        public RecoveryState FinalState { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string FailedVersion { get; set; } = string.Empty;
        public string RestoredVersion { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
