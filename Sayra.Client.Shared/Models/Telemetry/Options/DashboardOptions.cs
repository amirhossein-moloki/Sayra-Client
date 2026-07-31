using System.ComponentModel.DataAnnotations;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing local and remote admin dashboard feeds.
    /// </summary>
    public class DashboardOptions
    {
        /// <summary>
        /// Gets or sets the UI refresh update cycle frequency in seconds.
        /// </summary>
        [Range(1, 300, ErrorMessage = "RefreshIntervalSeconds must be between 1 and 300.")]
        public int RefreshIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// Gets or sets the maximum number of unhandled alerts displayed in dashboard listings.
        /// </summary>
        [Range(1, 500, ErrorMessage = "MaxVisibleAlerts must be between 1 and 500.")]
        public int MaxVisibleAlerts { get; set; } = 50;
    }
}
