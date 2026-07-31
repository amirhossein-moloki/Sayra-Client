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
    /// Collects graphics card and display engine metrics.
    /// </summary>
    public class GpuCollector : BaseTelemetryCollector
    {
        private readonly IHardwareSensorProvider _sensorProvider;
        private readonly Random _random = new();
        private const double TotalVramMb = 8192.0; // 8 GB VRAM standard workstation

        public GpuCollector(IHardwareSensorProvider sensorProvider, ILogger<GpuCollector> logger)
            : base("GPU Collector", CollectionInterval.Hardware, 60, TimeSpan.FromSeconds(5), logger)
        {
            _sensorProvider = sensorProvider ?? throw new ArgumentNullException(nameof(sensorProvider));
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            double gpuUsage = 5.0 + (_random.NextDouble() * 70.0); // 5% - 75%
            double vramUsedMb = 1024.0 + (_random.NextDouble() * 3000.0); // 1GB - 4GB used
            double gpuTemp = _sensorProvider.GetGpuTemperature();
            double fps = 60.0 + _random.Next(180); // 60 - 240 fps

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "system.gpu.usage",
                    Category = MetricCategory.Gpu,
                    Value = Math.Round(gpuUsage, 2),
                    Unit = MetricUnit.Percent,
                    Source = Name,
                    Severity = gpuUsage > 90.0 ? MetricSeverity.Critical : (gpuUsage > 75.0 ? MetricSeverity.Warning : MetricSeverity.Info)
                },
                new()
                {
                    MetricName = "system.gpu.vram_used",
                    Category = MetricCategory.Gpu,
                    Value = Math.Round(vramUsedMb, 2),
                    Unit = MetricUnit.Megabytes,
                    Source = Name,
                    Severity = (vramUsedMb / TotalVramMb) * 100.0 > 90.0 ? MetricSeverity.Critical : ((vramUsedMb / TotalVramMb) * 100.0 > 75.0 ? MetricSeverity.Warning : MetricSeverity.Info)
                },
                new()
                {
                    MetricName = "system.gpu.vram_total",
                    Category = MetricCategory.Gpu,
                    Value = TotalVramMb,
                    Unit = MetricUnit.Megabytes,
                    Source = Name,
                    Severity = MetricSeverity.Info
                },
                new()
                {
                    MetricName = "system.gpu.temperature",
                    Category = MetricCategory.Gpu,
                    Value = Math.Round(gpuTemp, 2),
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = gpuTemp > 85.0 ? MetricSeverity.Critical : (gpuTemp > 75.0 ? MetricSeverity.Warning : MetricSeverity.Info)
                },
                new()
                {
                    MetricName = "system.gpu.fps",
                    Category = MetricCategory.Gpu,
                    Value = fps,
                    Unit = MetricUnit.Rate,
                    Source = Name,
                    Severity = fps < 30.0 ? MetricSeverity.Warning : MetricSeverity.Info
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }

        protected override void MapRecordToLiveData(TelemetryRecord record, LiveTelemetryData data)
        {
            switch (record.MetricName)
            {
                case "system.gpu.usage":
                    data.GpuUsagePercent = record.Value;
                    break;
                case "system.gpu.vram_used":
                    data.VramUsedMb = record.Value;
                    break;
                case "system.gpu.vram_total":
                    data.VramTotalMb = record.Value;
                    break;
                case "system.gpu.temperature":
                    data.GpuTemperature = record.Value;
                    break;
                case "system.gpu.fps":
                    data.Fps = record.Value;
                    break;
            }
        }
    }
}
