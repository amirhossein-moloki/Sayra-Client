using System;

namespace Sayra.Client.Shared.Models.Recovery.Policies
{
    /// <summary>
    /// Reusable policy for managing data sampling intervals, window sizes, and smoothing factors.
    /// </summary>
    public class SamplingPolicy
    {
        /// <summary>
        /// Gets or sets the interval between samples.
        /// </summary>
        public TimeSpan SamplingInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the size of the rolling sample window.
        /// </summary>
        public int WindowSize { get; set; } = 10;

        /// <summary>
        /// Gets or sets the Exponential Moving Average (EMA) smoothing coefficient.
        /// Value must be between 0.0 and 1.0.
        /// </summary>
        public double EmaSmoothFactor { get; set; } = 0.2;
    }
}
