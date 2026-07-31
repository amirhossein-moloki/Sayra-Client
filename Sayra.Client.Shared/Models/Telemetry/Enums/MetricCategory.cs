namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Classifies the system resource category of tracked metrics.
    /// </summary>
    public enum MetricCategory
    {
        /// <summary>Processor resource usage.</summary>
        Cpu,
        /// <summary>System random access memory resource usage.</summary>
        Memory,
        /// <summary>Graphics processing unit resource usage.</summary>
        Gpu,
        /// <summary>Disk and storage subsystem resource usage.</summary>
        Disk,
        /// <summary>Network interface and transport speed usage.</summary>
        Network,
        /// <summary>System process details and diagnostics.</summary>
        Process,
        /// <summary>Active game runtime tracking details.</summary>
        Game,
        /// <summary>User workstation play sessions details.</summary>
        Session,
        /// <summary>Workstation policy enforcement details.</summary>
        Policy,
        /// <summary>Local ad and content plugins details.</summary>
        Plugin,
        /// <summary>Download speeds and states details.</summary>
        Download,
        /// <summary>Software and manifest updates pipeline details.</summary>
        Update,
        /// <summary>SQLite/SQLCipher persistence details.</summary>
        Database,
        /// <summary>Local Named Pipe IPC latency details.</summary>
        Ipc,
        /// <summary>System and administrator alert notification delivery details.</summary>
        Notification,
        /// <summary>Cloud or fleet configuration synchronization details.</summary>
        Sync,
        /// <summary>DirectX and WPF visual overlay status details.</summary>
        Overlay,
        /// <summary>Supervisor background worker watchdog details.</summary>
        Watchdog
    }
}
