namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Indicates the severity classification of individual telemetry records.
    /// </summary>
    public enum MetricSeverity
    {
        /// <summary>Standard diagnostic or status updates.</summary>
        Info,
        /// <summary>Minor deviations or warning indications.</summary>
        Warning,
        /// <summary>Active system failures or error states.</summary>
        Error,
        /// <summary>High priority or emergency failures.</summary>
        Critical
    }
}
