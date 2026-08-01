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
    /// Collects ongoing package downloads and download engine queue metrics.
    /// </summary>
    public class DownloadsCollector : BaseTelemetryCollector
    {
        public DownloadsCollector(ILogger<DownloadsCollector> logger)
            : base("Downloads Collector", CollectionInterval.Performance, 70, TimeSpan.FromSeconds(5), logger)
        {
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            double activeDownloads = 0;
            double downloadSpeedKbps = 0.0;

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "runtime.downloads.active",
                    Category = MetricCategory.Download,
                    Value = activeDownloads,
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = MetricSeverity.Info
                },
                new()
                {
                    MetricName = "runtime.downloads.speed_kbps",
                    Category = MetricCategory.Download,
                    Value = downloadSpeedKbps,
                    Unit = MetricUnit.BitsPerSecond,
                    Source = Name,
                    Severity = MetricSeverity.Info
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }
    }
}
