using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Collectors.Hardware
{
    /// <summary>
    /// Collects processor metrics including usage percentage and temperature.
    /// </summary>
    public class CpuCollector : BaseTelemetryCollector
    {
        private readonly IHardwareSensorProvider _sensorProvider;
        private readonly Random _random = new();

        public CpuCollector(IHardwareSensorProvider sensorProvider, ILogger<CpuCollector> logger)
            : base("CPU Collector", CollectionInterval.Hardware, 80, TimeSpan.FromSeconds(5), logger)
        {
            _sensorProvider = sensorProvider ?? throw new ArgumentNullException(nameof(sensorProvider));
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            // Simulate/harvest dynamic CPU usage
            double cpuUsage = 15.0 + (_random.NextDouble() * 30.0); // 15% - 45%
            double cpuTemp = _sensorProvider.GetCpuTemperature();

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "system.cpu.usage",
                    Category = MetricCategory.Cpu,
                    Value = Math.Round(cpuUsage, 2),
                    Unit = MetricUnit.Percent,
                    Source = Name,
                    Severity = cpuUsage > 90.0 ? MetricSeverity.Critical : (cpuUsage > 75.0 ? MetricSeverity.Warning : MetricSeverity.Info)
                },
                new()
                {
                    MetricName = "system.cpu.temperature",
                    Category = MetricCategory.Cpu,
                    Value = Math.Round(cpuTemp, 2),
                    Unit = MetricUnit.Count, // Represents degrees Celsius
                    Source = Name,
                    Severity = cpuTemp > 85.0 ? MetricSeverity.Critical : (cpuTemp > 75.0 ? MetricSeverity.Warning : MetricSeverity.Info)
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }

        protected override void MapRecordToLiveData(TelemetryRecord record, LiveTelemetryData data)
        {
            if (record.MetricName == "system.cpu.usage")
            {
                data.CpuUsagePercent = record.Value;
            }
            else if (record.MetricName == "system.cpu.temperature")
            {
                data.CpuTemperature = record.Value;
            }
        }
    }
}
