using System;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Contract for preventing duplicate alerts through fingerprinting and time-window evaluation.
    /// </summary>
    public interface IAlertDeduplicationProvider
    {
        /// <summary>
        /// Generates a unique deduplication fingerprint for the specified alert record.
        /// </summary>
        string GenerateFingerprint(AlertRecord alert);

        /// <summary>
        /// Determines if the new alert is a duplicate of the existing active alert.
        /// </summary>
        bool IsDuplicate(AlertRecord existing, AlertRecord newAlert, TimeSpan timeWindow);
    }
}
