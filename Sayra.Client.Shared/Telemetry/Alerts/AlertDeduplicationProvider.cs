using System;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Alerts
{
    public class AlertDeduplicationProvider : IAlertDeduplicationProvider
    {
        public string GenerateFingerprint(AlertRecord alert)
        {
            return $"{alert.Subsystem}_{alert.Name}_{alert.Category}".ToUpperInvariant();
        }

        public bool IsDuplicate(AlertRecord existing, AlertRecord newAlert, TimeSpan timeWindow)
        {
            if (existing.Resolved)
                return false;

            string existingFingerprint = GenerateFingerprint(existing);
            string newFingerprint = GenerateFingerprint(newAlert);

            if (existingFingerprint != newFingerprint)
                return false;

            if (timeWindow > TimeSpan.Zero)
            {
                return (newAlert.Timestamp - existing.Timestamp) <= timeWindow;
            }

            return true;
        }
    }
}
