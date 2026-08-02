using System;
using System.Collections.Concurrent;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Telemetry.Alerts
{
    public class AlertSuppressionProvider : IAlertSuppressionProvider
    {
        private readonly ConcurrentDictionary<string, DateTime> _manualSuppressions = new();

        public bool IsSuppressed(AlertRecord alert, AlertPolicyConfig policy, DateTime now)
        {
            if (policy == null) return false;

            if (_manualSuppressions.TryGetValue(alert.AlertId, out var manualUntil))
            {
                if (now < manualUntil)
                    return true;
                _manualSuppressions.TryRemove(alert.AlertId, out _);
            }

            if (_manualSuppressions.TryGetValue(alert.Name, out var nameUntil))
            {
                if (now < nameUntil)
                    return true;
                _manualSuppressions.TryRemove(alert.Name, out _);
            }

            if (policy.Suppression != null)
            {
                if (policy.Suppression.IsSuppressed)
                    return true;

                if (policy.Suppression.SuppressUntil.HasValue && now < policy.Suppression.SuppressUntil.Value)
                    return true;

                if (policy.Suppression.SuppressedSubsystems != null &&
                    policy.Suppression.SuppressedSubsystems.Contains(alert.Subsystem.ToString()))
                {
                    return true;
                }

                if (policy.Suppression.SuppressedRules != null &&
                    policy.Suppression.SuppressedRules.Contains(alert.Name))
                {
                    return true;
                }

                if (policy.Suppression.MaintenanceWindowOnly)
                {
                    return true;
                }
            }

            return false;
        }

        public void SuppressManual(string keyOrId, TimeSpan? duration = null)
        {
            var until = DateTime.UtcNow.Add(duration ?? TimeSpan.FromHours(1));
            _manualSuppressions[keyOrId] = until;
        }
    }
}
