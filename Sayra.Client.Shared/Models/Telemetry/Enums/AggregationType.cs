namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Enumerates metrics mathematical aggregation strategies.
    /// </summary>
    public enum AggregationType
    {
        /// <summary>Simple incremental counter value.</summary>
        Counter,
        /// <summary>Instantaneous resource measurement value.</summary>
        Gauge,
        /// <summary>Frequency distribution of numerical readings.</summary>
        Histogram,
        /// <summary>Latency measurement tracking durations.</summary>
        Timer,
        /// <summary>Occurrences count per standard unit time.</summary>
        Rate,
        /// <summary>Statistical percentile values (e.g. 95th, 99th).</summary>
        Percentile,
        /// <summary>Rolling consolidated average of readings.</summary>
        RollingAverage,
        /// <summary>Exponential moving average calculating resource trends.</summary>
        MovingAverage
    }
}
