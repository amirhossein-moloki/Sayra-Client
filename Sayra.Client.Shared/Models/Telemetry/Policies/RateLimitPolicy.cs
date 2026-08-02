using System;

namespace Sayra.Client.Shared.Models.Telemetry.Policies
{
    /// <summary>
    /// Reusable policy defining alerting rate limits over configured time windows.
    /// </summary>
    public class RateLimitPolicy
    {
        public int MaxAlertsPerWindow { get; set; } = 5;
        public int WindowSeconds { get; set; } = 60;
    }
}
