using System.ComponentModel.DataAnnotations;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing long-term metrics database table retention limits.
    /// </summary>
    public class RetentionOptions
    {
        /// <summary>
        /// Gets or sets the retention window in days before older telemetry records are pruned.
        /// </summary>
        [Range(1, 365, ErrorMessage = "RetentionDays must be between 1 and 365.")]
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// Gets or sets the target retention consolidated frequency interval (e.g. daily, weekly).
        /// </summary>
        public RetentionPolicyType PolicyType { get; set; } = RetentionPolicyType.Daily;
    }
}
