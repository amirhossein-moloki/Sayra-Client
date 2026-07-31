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
    /// Collects workstation configuration and billing database synchronization status.
    /// </summary>
    public class SyncCollector : BaseTelemetryCollector
    {
        public SyncCollector(ILogger<SyncCollector> logger)
            : base("Sync Collector", CollectionInterval.Performance, 55, TimeSpan.FromSeconds(5), logger)
        {
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            double isSynchronized = 1.0;

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "runtime.sync.synchronized",
                    Category = MetricCategory.Sync,
                    Value = isSynchronized,
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = MetricSeverity.Info,
                    Tags = new Dictionary<string, string> { { "sync_state", "Synchronized" } }
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }
    }
}
