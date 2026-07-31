namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Indicates the evaluated operational health rating of system diagnostics.
    /// </summary>
    public enum DiagnosticStatus
    {
        /// <summary>Subsystem is fully operational and healthy.</summary>
        Healthy,
        /// <summary>Subsystem is exhibiting warning deviations.</summary>
        Warning,
        /// <summary>Subsystem performance or status is degraded.</summary>
        Degraded,
        /// <summary>Subsystem is in a failed or critical status.</summary>
        Critical
    }
}
