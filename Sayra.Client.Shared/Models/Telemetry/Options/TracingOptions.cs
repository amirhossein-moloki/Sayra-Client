using System.ComponentModel.DataAnnotations;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing distributed transaction tracing spans.
    /// </summary>
    public class TracingOptions
    {
        /// <summary>
        /// Gets or sets the probabilistic trace sampling likelihood (0.0 to 1.0).
        /// </summary>
        [Range(0.0, 1.0, ErrorMessage = "SamplingProbability must be between 0.0 and 1.0.")]
        public double SamplingProbability { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets the maximum nested call depth tracked in distributed context maps.
        /// </summary>
        [Range(1, 100, ErrorMessage = "MaxTraceDepth must be between 1 and 100.")]
        public int MaxTraceDepth { get; set; } = 10;

        /// <summary>
        /// Gets or sets the execution span cancellation limit in milliseconds.
        /// </summary>
        [Range(100, 60000, ErrorMessage = "RequestTimeoutMilliseconds must be between 100 and 60000.")]
        public int RequestTimeoutMilliseconds { get; set; } = 5000;
    }
}
