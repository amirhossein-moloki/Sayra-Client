namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Enumerates standard measurement units of telemetry metrics.
    /// </summary>
    public enum MetricUnit
    {
        /// <summary>Percentage values (0.0 to 100.0).</summary>
        Percent,
        /// <summary>Raw memory/disk size in bytes.</summary>
        Bytes,
        /// <summary>Memory/disk size in megabytes.</summary>
        Megabytes,
        /// <summary>Memory/disk size in gigabytes.</summary>
        Gigabytes,
        /// <summary>Time span in milliseconds.</summary>
        Milliseconds,
        /// <summary>Time span in seconds.</summary>
        Seconds,
        /// <summary>Data rate in bits per second.</summary>
        BitsPerSecond,
        /// <summary>Standard integer counts.</summary>
        Count,
        /// <summary>Frequency rate of occurrences per unit time.</summary>
        Rate
    }
}
