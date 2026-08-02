using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Telemetry.Alerts
{
    public class AlertEscalationProvider : IAlertEscalationProvider
    {
        private readonly ConcurrentDictionary<string, int> _recurrenceCounters = new();

        public Task<AlertRecord?> CheckAndEscalateAsync(AlertRecord alert, AlertPolicyConfig policy, CancellationToken cancellationToken = default)
        {
            if (policy == null || policy.Escalation == null || !policy.Escalation.Enabled)
                return Task.FromResult<AlertRecord?>(null);

            bool shouldEscalate = false;

            var durationActive = DateTime.UtcNow - alert.Timestamp;
            if (durationActive >= TimeSpan.FromMinutes(policy.Escalation.DurationMinutesBeforeEscalation))
            {
                shouldEscalate = true;
            }

            string key = $"{alert.Subsystem}_{alert.Name}";
            _recurrenceCounters.AddOrUpdate(key, 1, (k, val) => val + 1);
            if (_recurrenceCounters.TryGetValue(key, out var count) && count >= policy.Escalation.FrequencyThreshold)
            {
                shouldEscalate = true;
                _recurrenceCounters.TryRemove(key, out _);
            }

            if (shouldEscalate && !alert.Escalated)
            {
                var currentPriority = alert.Priority;
                var nextPriority = currentPriority switch
                {
                    AlertPriority.Info => AlertPriority.Warning,
                    AlertPriority.Warning => AlertPriority.Critical,
                    _ => AlertPriority.Emergency
                };

                if (Enum.TryParse<AlertPriority>(policy.Escalation.EscalationPriority, out var configuredPriority))
                {
                    nextPriority = configuredPriority;
                }

                var escalatedAlert = alert with
                {
                    Escalated = true,
                    EscalatedAt = DateTime.UtcNow,
                    Priority = nextPriority,
                    Status = AlertStatus.Escalated
                };

                return Task.FromResult<AlertRecord?>(escalatedAlert);
            }

            return Task.FromResult<AlertRecord?>(null);
        }
    }
}
