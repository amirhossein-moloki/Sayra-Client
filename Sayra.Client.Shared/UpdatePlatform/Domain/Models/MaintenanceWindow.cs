using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents an enterprise maintenance window governing when update actions can occur.
    /// </summary>
    public class MaintenanceWindow
    {
        /// <summary>
        /// Days of the week when maintenance operations are permitted.
        /// </summary>
        public List<DayOfWeek> AllowedDays { get; set; } = new();

        /// <summary>
        /// Start of the daily window.
        /// </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>
        /// End of the daily window.
        /// </summary>
        public TimeSpan EndTime { get; set; }

        /// <summary>
        /// Exact dates excluded from maintenance operations (e.g. public holidays).
        /// </summary>
        public List<DateTime> HolidayExclusions { get; set; } = new();

        /// <summary>
        /// Time zone governing the maintenance window calculations (e.g. "UTC").
        /// </summary>
        public string TimeZoneId { get; set; } = "UTC";

        /// <summary>
        /// Indicates if forced upgrades bypass maintenance windows.
        /// </summary>
        public bool AllowForcedUpgrades { get; set; } = true;
    }
}
