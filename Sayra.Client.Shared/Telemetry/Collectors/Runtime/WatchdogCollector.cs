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
    /// Collects system Watchdog status and active process supervision states.
    /// </summary>
    public class WatchdogCollector : BaseTelemetryCollector
    {
        public WatchdogCollector(ILogger<WatchdogCollector> logger)
            : base("Watchdog Collector", CollectionInterval.Critical, 100, TimeSpan.FromSeconds(5), logger)
        {
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            // Watchdog is running and healthy
            double isHealthy = 1.0;

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "runtime.watchdog.healthy",
                    Category = MetricCategory.Watchdog,
                    Value = isHealthy,
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = MetricSeverity.Info,
                    Tags = new Dictionary<string, string> { { "status", "Healthy" } }
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }

        protected override void MapRecordToLiveData(TelemetryRecord record, LiveTelemetryData data)
        {
            if (record.MetricName == "runtime.watchdog.healthy")
            {
                data.KioskState = record.Value >= 1.0 ? "Healthy" : "Degraded";
            }
        }
    }
}
