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
    /// Collects loaded application plugins and extensions status details.
    /// </summary>
    public class PluginsCollector : BaseTelemetryCollector
    {
        public PluginsCollector(ILogger<PluginsCollector> logger)
            : base("Plugins Collector", CollectionInterval.Performance, 80, TimeSpan.FromSeconds(5), logger)
        {
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            // Simulate plugins count (could scan directories or plugins registration service if loaded)
            int pluginCount = 3;

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "runtime.plugins.count",
                    Category = MetricCategory.Plugin,
                    Value = pluginCount,
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = MetricSeverity.Info,
                    Tags = new Dictionary<string, string> { { "loaded_plugins", "AdCarousel,GameOverlay,BillingBridge" } }
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }
    }
}
