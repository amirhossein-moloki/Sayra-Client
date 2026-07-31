using System.ComponentModel.DataAnnotations;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing workstation telemetry recording behavior.
    /// </summary>
    public class TelemetryOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether workstation telemetry capture is enabled.
        /// </summary>
        public bool EnableTelemetry { get; set; } = true;

        /// <summary>
        /// Gets or sets the probabilistic telemetry sampling rate (0.0 to 1.0).
        /// </summary>
        [Range(0.0, 1.0, ErrorMessage = "SamplingRate must be between 0.0 and 1.0.")]
        public double SamplingRate { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets the maximum buffer capacity before flushing telemetry records.
        /// </summary>
        [Range(10, 10000, ErrorMessage = "BufferSize must be between 10 and 10000.")]
        public int BufferSize { get; set; } = 1000;
    }
}
