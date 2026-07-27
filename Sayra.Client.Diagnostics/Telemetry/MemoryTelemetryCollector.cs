using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Diagnostics.Telemetry
{
    public class MemoryTelemetryCollector : ITelemetryCollector
    {
        private readonly IMemoryProvider _memoryProvider;
        private readonly ILogger<MemoryTelemetryCollector> _logger;

        public MemoryTelemetryCollector(IMemoryProvider memoryProvider, ILogger<MemoryTelemetryCollector> logger)
        {
            _memoryProvider = memoryProvider ?? throw new ArgumentNullException(nameof(memoryProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CollectAsync(LiveTelemetryData data, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Collecting Memory telemetry...");
                var memInfo = await _memoryProvider.GetMemoryAsync(cancellationToken);
                data.RamTotalMb = Math.Round(memInfo.InstalledMemory / (1024.0 * 1024.0), 1);
                data.RamUsedMb = Math.Round(memInfo.UsedMemory / (1024.0 * 1024.0), 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect Memory telemetry.");
                data.RamTotalMb = 16384.0;
                data.RamUsedMb = 4096.0;
            }
        }
    }
}
