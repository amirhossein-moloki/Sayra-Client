using System;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Centralized parent configuration model aggregating all enterprise resilience and self-healing subsystem options.
    /// Supports schema version tracking and dynamic runtime reload.
    /// </summary>
    public class ResilienceConfiguration
    {
        /// <summary>
        /// Gets or sets the schema version of the configuration structure.
        /// </summary>
        public string SchemaVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the metadata description of this configuration profile.
        /// </summary>
        public string Description { get; set; } = "SAYRA Enterprise Resilience and Policy Framework Profile";

        /// <summary>
        /// Gets or sets the health monitoring configuration options.
        /// </summary>
        public HealthMonitorOptions HealthMonitor { get; set; } = new();

        /// <summary>
        /// Gets or sets the self-healing engine configuration options.
        /// </summary>
        public SelfHealingOptions SelfHealing { get; set; } = new();

        /// <summary>
        /// Gets or sets the recovery policies configuration options.
        /// </summary>
        public RecoveryPolicyOptions RecoveryPolicy { get; set; } = new();

        /// <summary>
        /// Gets or sets the crash recovery manager configuration options.
        /// </summary>
        public CrashRecoveryOptions CrashRecovery { get; set; } = new();

        /// <summary>
        /// Gets or sets the resource monitoring configuration options.
        /// </summary>
        public ResourceMonitorOptions ResourceMonitor { get; set; } = new();

        /// <summary>
        /// Gets or sets the security hardening configuration options.
        /// </summary>
        public SecurityHardeningOptions SecurityHardening { get; set; } = new();

        /// <summary>
        /// Gets or sets the graceful shutdown configuration options.
        /// </summary>
        public GracefulShutdownOptions GracefulShutdown { get; set; } = new();

        /// <summary>
        /// Gets or sets the recovery diagnostics configuration options.
        /// </summary>
        public RecoveryDiagnosticsOptions Diagnostics { get; set; } = new();

        /// <summary>
        /// Gets or sets the watchdog background daemon configuration options.
        /// </summary>
        public WatchdogOptions Watchdog { get; set; } = new();
    }
}
