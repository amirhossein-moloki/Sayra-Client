using System;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Contract for evaluating temporary, permanent, maintenance, subsystem, rule, or manual alert suppressions.
    /// </summary>
    public interface IAlertSuppressionProvider
    {
        /// <summary>
        /// Determines if the alert is currently suppressed under the specified policy.
        /// </summary>
        bool IsSuppressed(AlertRecord alert, AlertPolicyConfig policy, DateTime now);

        /// <summary>
        /// Explicitly suppresses an alert manually for an optional duration.
        /// </summary>
        void SuppressManual(string keyOrId, TimeSpan? duration = null);
    }
}
