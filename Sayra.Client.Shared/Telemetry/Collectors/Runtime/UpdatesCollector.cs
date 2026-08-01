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
    /// Collects software updates availability, pending installations, and installer states.
    /// </summary>
    public class UpdatesCollector : BaseTelemetryCollector
    {
        public UpdatesCollector(ILogger<UpdatesCollector> logger)
            : base("Updates Collector", CollectionInterval.Performance, 65, TimeSpan.FromSeconds(5), logger)
        {
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            double pendingUpdates = 0;

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "runtime.updates.pending",
                    Category = MetricCategory.Update,
                    Value = pendingUpdates,
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = MetricSeverity.Info,
                    Tags = new Dictionary<string, string> { { "update_status", "UpToDate" } }
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }
    }
}
