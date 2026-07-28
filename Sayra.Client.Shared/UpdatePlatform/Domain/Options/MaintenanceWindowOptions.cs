using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Options
{
    /// <summary>
    /// Configuration options for Maintenance Windows.
    /// </summary>
    public class MaintenanceWindowOptions
    {
        public string StartTimeUtc { get; set; } = "03:00:00";
        public string EndTimeUtc { get; set; } = "05:00:00";
        public List<string> AllowedDays { get; set; } = new() { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        public bool AllowForcedUpgrades { get; set; } = true;
        public int MaxOccupancyPercentage { get; set; } = 5;
        public List<string> HolidayExclusions { get; set; } = new();
        public string TimeZoneId { get; set; } = "UTC";
    }
}
