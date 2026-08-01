namespace Sayra.Client.Shared.Telemetry.Diagnostics
{
    /// <summary>
    /// Represents the evaluation health status of a diagnostic check or subsystem.
    /// </summary>
    public enum DiagnosticHealthStatus
    {
        /// <summary>
        /// Subsystem is fully operational and within ideal parameters.
        /// </summary>
        Healthy,

        /// <summary>
        /// Subsystem has warning indicators but continues to operate.
        /// </summary>
        Warning,

        /// <summary>
        /// Subsystem performance is degraded.
        /// </summary>
        Degraded,

        /// <summary>
        /// Subsystem has suffered a critical failure.
        /// </summary>
        Critical,

        /// <summary>
        /// Subsystem is completely offline or unreachable.
        /// </summary>
        Offline,

        /// <summary>
        /// Subsystem health could not be determined.
        /// </summary>
        Unknown
    }
}
