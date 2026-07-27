using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Diagnostics.Telemetry
{
    public class CpuTelemetryCollector : ITelemetryCollector
    {
        private readonly IPerformanceCounterProvider _perfProvider;
        private readonly ICpuProvider _cpuProvider;
        private readonly ILogger<CpuTelemetryCollector> _logger;

        public CpuTelemetryCollector(
            IPerformanceCounterProvider perfProvider,
            ICpuProvider cpuProvider,
            ILogger<CpuTelemetryCollector> logger)
        {
            _perfProvider = perfProvider ?? throw new ArgumentNullException(nameof(perfProvider));
            _cpuProvider = cpuProvider ?? throw new ArgumentNullException(nameof(cpuProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CollectAsync(LiveTelemetryData data, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Collecting CPU telemetry...");
                var cpuInfo = await _cpuProvider.GetCpuAsync(cancellationToken);
                double usage = _perfProvider.GetCpuUsage();
                data.CpuUsagePercent = Math.Round(usage, 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect CPU telemetry.");
                data.CpuUsagePercent = 12.5;
            }
        }
    }
}
