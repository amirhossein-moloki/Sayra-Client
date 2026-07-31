namespace Sayra.Client.Shared.Models.Telemetry.Enums
{
    /// <summary>
    /// Enumerates historical metric data cleanup and aggregation frequencies.
    /// </summary>
    public enum RetentionPolicyType
    {
        /// <summary>Metrics consolidated on an hourly basis.</summary>
        Hourly,
        /// <summary>Metrics consolidated on a daily basis.</summary>
        Daily,
        /// <summary>Metrics consolidated on a weekly basis.</summary>
        Weekly,
        /// <summary>Metrics consolidated on a monthly basis.</summary>
        Monthly
    }
}
