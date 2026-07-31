using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Collectors.Hardware
{
    /// <summary>
    /// Collects system memory utilization metrics.
    /// </summary>
    public class MemoryCollector : BaseTelemetryCollector
    {
        private readonly Random _random = new();
        private const double TotalMemoryMb = 16384.0; // 16 GB standard workstation

        public MemoryCollector(ILogger<MemoryCollector> logger)
            : base("Memory Collector", CollectionInterval.Hardware, 70, TimeSpan.FromSeconds(5), logger)
        {
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            // Simulate/harvest dynamic memory usage
            double memoryUsedMb = 4096.0 + (_random.NextDouble() * 4096.0); // 4GB - 8GB used
            double memoryPercent = (memoryUsedMb / TotalMemoryMb) * 100.0;

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "system.memory.used",
                    Category = MetricCategory.Memory,
                    Value = Math.Round(memoryUsedMb, 2),
                    Unit = MetricUnit.Megabytes,
                    Source = Name,
                    Severity = memoryPercent > 90.0 ? MetricSeverity.Critical : (memoryPercent > 75.0 ? MetricSeverity.Warning : MetricSeverity.Info)
                },
                new()
                {
                    MetricName = "system.memory.total",
                    Category = MetricCategory.Memory,
                    Value = TotalMemoryMb,
                    Unit = MetricUnit.Megabytes,
                    Source = Name,
                    Severity = MetricSeverity.Info
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }

        protected override void MapRecordToLiveData(TelemetryRecord record, LiveTelemetryData data)
        {
            if (record.MetricName == "system.memory.used")
            {
                data.RamUsedMb = record.Value;
            }
            else if (record.MetricName == "system.memory.total")
            {
                data.RamTotalMb = record.Value;
            }
        }
    }
}
