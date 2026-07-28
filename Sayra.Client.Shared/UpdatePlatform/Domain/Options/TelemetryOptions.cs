namespace Sayra.Client.Shared.UpdatePlatform.Domain.Options
{
    /// <summary>
    /// Configuration options for the Update Telemetry Engine.
    /// </summary>
    public class TelemetryOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether telemetry is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of telemetry events to buffer offline.
        /// </summary>
        public int QueueLimit { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the reporting interval in seconds for background processing.
        /// </summary>
        public int ReportingIntervalSeconds { get; set; } = 5;
    }
}
