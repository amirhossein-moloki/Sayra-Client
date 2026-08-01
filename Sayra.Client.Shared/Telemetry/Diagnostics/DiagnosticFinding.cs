namespace Sayra.Client.Shared.Telemetry.Diagnostics
{
    /// <summary>
    /// Represents a specific, structured finding detected by an individual diagnostic module.
    /// Used by the Recommendation Engine to generate actionable optimization or remediation recommendations.
    /// </summary>
    public record DiagnosticFinding
    {
        /// <summary>
        /// Gets the identifying key of the finding (e.g. CpuUsageLimitExceeded, LowAvailableRam).
        /// </summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>
        /// Gets the measured or evaluated value of the finding.
        /// </summary>
        public string Value { get; init; } = string.Empty;

        /// <summary>
        /// Gets the subsystem name where this finding was detected.
        /// </summary>
        public string Subsystem { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether this finding represents an anomaly or failure.
        /// </summary>
        public bool IsAnomaly { get; init; }

        /// <summary>
        /// Gets descriptive details about the finding context.
        /// </summary>
        public string Details { get; init; } = string.Empty;
    }
}
