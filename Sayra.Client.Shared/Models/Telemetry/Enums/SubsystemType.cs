namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Enumerates individual subsystems of the SAYRA workstation client.
    /// </summary>
    public enum SubsystemType
    {
        /// <summary>Authentication and login workflows.</summary>
        Authentication,
        /// <summary>SQLite database and SQLCipher encryption.</summary>
        Database,
        /// <summary>TCP Socket client and secure transport layer.</summary>
        Network,
        /// <summary>Local Named Pipe IPC pipeline.</summary>
        IPC,
        /// <summary>Local popups and notification queues.</summary>
        Notifications,
        /// <summary>Update package file downloader engine.</summary>
        Downloads,
        /// <summary>Atomic package replacement installation pipeline.</summary>
        Updates,
        /// <summary>Ad campaigns and media storage.</summary>
        Media,
        /// <summary>Game and client extension plugins.</summary>
        Plugins,
        /// <summary>Observability, logging, and metrics telemetry.</summary>
        Telemetry,
        /// <summary>Self-healing, rollback, and crash recovery orchestration.</summary>
        Recovery,
        /// <summary>Kiosk security, token checks, and anti-tampering audits.</summary>
        Security,
        /// <summary>Group policies and workstation configurations.</summary>
        Policies,
        /// <summary>Deadlock, queue length, and thread freezes monitor.</summary>
        Watchdog,
        /// <summary>DirectX overlay gameplay window renderer.</summary>
        Overlay,
        /// <summary>Cloud server ad and configuration sync worker.</summary>
        Synchronization
    }
}
