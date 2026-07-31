namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Indicates the emergency escalation and prioritization of raised alerts.
    /// </summary>
    public enum AlertPriority
    {
        /// <summary>Standard operational details.</summary>
        Info,
        /// <summary>System warning that requires attention.</summary>
        Warning,
        /// <summary>Critical error affecting specific subsystem functionality.</summary>
        Critical,
        /// <summary>Emergency situation causing workstation-wide operational degradation.</summary>
        Emergency
    }
}
