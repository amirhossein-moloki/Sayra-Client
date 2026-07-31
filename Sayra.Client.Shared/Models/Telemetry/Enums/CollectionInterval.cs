namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Defines telemetry and metrics collection intervals in seconds.
    /// </summary>
    public enum CollectionInterval
    {
        /// <summary>Critical performance and process metrics (5 seconds).</summary>
        Critical = 5,
        /// <summary>General application latency metrics (15 seconds).</summary>
        Performance = 15,
        /// <summary>Hardware utilization and resource snapshots (30 seconds).</summary>
        Hardware = 30,
        /// <summary>Disk and persistent storage capacity metrics (60 seconds).</summary>
        Storage = 60,
        /// <summary>Downsampled historical trend rollups (300 seconds / 5 minutes).</summary>
        Historical = 300
    }
}
