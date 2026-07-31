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
    /// Collects status details for the WPF gameplay overlay subsystem.
    /// </summary>
    public class OverlayCollector : BaseTelemetryCollector
    {
        public OverlayCollector(ILogger<OverlayCollector> logger)
            : base("Overlay Collector", CollectionInterval.Performance, 45, TimeSpan.FromSeconds(5), logger)
        {
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            double isOverlayActive = 1.0;

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "runtime.overlay.active",
                    Category = MetricCategory.Overlay,
                    Value = isOverlayActive,
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = MetricSeverity.Info,
                    Tags = new Dictionary<string, string> { { "overlay_state", "Visible" } }
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }
    }
}
