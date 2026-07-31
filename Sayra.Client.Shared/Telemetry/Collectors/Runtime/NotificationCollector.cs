using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Collectors.Runtime
{
    /// <summary>
    /// Collects interactive notifications and central messaging queue metrics.
    /// </summary>
    public class NotificationCollector : BaseTelemetryCollector
    {
        public NotificationCollector(ILogger<NotificationCollector> logger)
            : base("Notification Collector", CollectionInterval.Performance, 50, TimeSpan.FromSeconds(5), logger)
        {
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            double pendingNotifications = 0;

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "runtime.notifications.pending",
                    Category = MetricCategory.Notification,
                    Value = pendingNotifications,
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = MetricSeverity.Info
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }
    }
}
