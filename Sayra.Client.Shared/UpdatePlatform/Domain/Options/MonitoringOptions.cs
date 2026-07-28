namespace Sayra.Client.Shared.UpdatePlatform.Domain.Options
{
    /// <summary>
    /// Configuration options for the Update Health Monitor.
    /// </summary>
    public class MonitoringOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether health monitoring is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum required free storage in bytes.
        /// </summary>
        public long MinStorageBytes { get; set; } = 104857600; // 100 MB

        /// <summary>
        /// Gets or sets the health check interval in minutes.
        /// </summary>
        public int CheckIntervalMinutes { get; set; } = 15;
    }
}
