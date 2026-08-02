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
        Escalated,
        /// <summary>The alert is created.</summary>
        Created,
        /// <summary>The alert is suppressed.</summary>
        Suppressed,
        /// <summary>The alert has been recovered.</summary>
        Recovered,
        /// <summary>The alert has expired.</summary>
        Expired,
        /// <summary>The alert has been closed.</summary>
        Closed
    }
}
