namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Indicates the active workflow state of raised workstation alerts.
    /// </summary>
    public enum AlertStatus
    {
        /// <summary>The alert is newly triggered and active.</summary>
        Active,
        /// <summary>The alert was seen and acknowledged by an administrator.</summary>
        Acknowledged,
        /// <summary>The underlying issue was successfully addressed and resolved.</summary>
        Resolved,
        /// <summary>The unresolved alert was escalated to higher-tier notifications.</summary>
        Escalated
    }
}
