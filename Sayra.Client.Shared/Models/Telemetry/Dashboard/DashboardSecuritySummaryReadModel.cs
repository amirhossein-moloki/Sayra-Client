using System;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents an immutable summary of workstation security posture, policy compliance, and integrity validation results.
    /// </summary>
    public record DashboardSecuritySummaryReadModel
    {
        /// <summary>
        /// Gets the total count of security violations or integrity alerts recorded.
        /// </summary>
        public int SecurityViolationsCount { get; init; }

        /// <summary>
        /// Gets the compliance rating of policy configuration applications (0.0 to 100.0).
        /// </summary>
        public double PolicyCompliancePercent { get; init; }

        /// <summary>
        /// Gets the current status of the Anti-Tamper system (e.g., "Enabled", "Disabled", "Error").
        /// </summary>
        public string AntiTamperStatus { get; init; } = "Enabled";

        /// <summary>
        /// Gets the current kiosk security state (e.g., "Locked", "Unlocked", "KioskActive").
        /// </summary>
        public string KioskSecurityStatus { get; init; } = "Locked";

        /// <summary>
        /// Gets the SQLCipher database encryption status (e.g., "Encrypted", "Unencrypted", "Locked").
        /// </summary>
        public string DatabaseEncryptionStatus { get; init; } = "Encrypted";

        /// <summary>
        /// Gets a value indicating whether full system cryptographic and signature integrity is successfully verified.
        /// </summary>
        public bool SystemIntegrityVerified { get; init; } = true;

        /// <summary>
        /// Gets the exact timestamp when this read model was generated.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
