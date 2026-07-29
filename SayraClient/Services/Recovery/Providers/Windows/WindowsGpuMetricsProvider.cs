using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Recovery.Providers;

namespace SayraClient.Services.Recovery.Providers.Windows
{
    public class WindowsGpuMetricsProvider : IGpuMetricsProvider
    {
        private readonly ILogger<WindowsGpuMetricsProvider> _logger;
        private readonly double _simulatedGpuUsage = 5.0; // 5% baseline
        private readonly double? _simulatedTemperature = 45.0; // 45C baseline

        public WindowsGpuMetricsProvider(ILogger<WindowsGpuMetricsProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<double> GetGpuUsagePercentageAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_simulatedGpuUsage);
        }

        public Task<double?> GetHardwareTemperatureCelsiusAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_simulatedTemperature);
        }
    }
}
