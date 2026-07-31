namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Enumerates visual telemetry widgets on the local/remote administrator console.
    /// </summary>
    public enum DashboardWidgetType
    {
        /// <summary>Display active workstation machine connections count.</summary>
        LiveMachines,
        /// <summary>Display logged-on workstation users count.</summary>
        OnlineUsers,
        /// <summary>Display currently running game processes count.</summary>
        RunningGames,
        /// <summary>Display average processor resource load percent.</summary>
        CpuUsage,
        /// <summary>Display average RAM utilization percent.</summary>
        MemoryUsage,
        /// <summary>Display background worker or system fault occurrences count.</summary>
        Failures,
        /// <summary>Display active unhandled alert records list.</summary>
        Alerts,
        /// <summary>Display active download pipeline bytes transfer speed.</summary>
        Downloads,
        /// <summary>Display pending software update packages count.</summary>
        Updates,
        /// <summary>Display socket connection status indicators.</summary>
        NetworkStatus,
        /// <summary>Display policy applications compliance rating.</summary>
        PolicyCompliance,
        /// <summary>Display ongoing self-healing recovery actions summary.</summary>
        RecoveryStatus,
        /// <summary>Display active security violations count.</summary>
        SecurityStatus
    }
}
