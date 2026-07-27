using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Diagnostics.Telemetry
{
    public class HardwareHealthCollector : ITelemetryCollector
    {
        private readonly IHardwareSensorProvider? _sensorProvider;
        private readonly ILogger<HardwareHealthCollector> _logger;

        public HardwareHealthCollector(ILogger<HardwareHealthCollector> logger, IHardwareSensorProvider? sensorProvider = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sensorProvider = sensorProvider;
        }

        public Task CollectAsync(LiveTelemetryData data, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_sensorProvider == null || !_sensorProvider.IsAvailable)
                {
                    data.CpuTemperature = 0;
                    data.GpuTemperature = 0;
                    data.FanSpeed = 0;
                    return Task.CompletedTask;
                }

                data.CpuTemperature = _sensorProvider.GetCpuTemperature();
                data.GpuTemperature = _sensorProvider.GetGpuTemperature();
                data.FanSpeed = _sensorProvider.GetFanSpeed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect Hardware Health telemetry.");
                data.CpuTemperature = 0;
                data.GpuTemperature = 0;
                data.FanSpeed = 0;
            }
            return Task.CompletedTask;
        }
    }
}
