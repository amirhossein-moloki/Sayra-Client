using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Recovery.Providers;

namespace SayraClient.Services.Recovery.Providers.Windows
{
    public class WindowsNetworkMetricsProvider : INetworkMetricsProvider
    {
        private readonly ILogger<WindowsNetworkMetricsProvider> _logger;
        private readonly double _simulatedNetworkIo = 1024 * 100; // 100 KB/s baseline

        public WindowsNetworkMetricsProvider(ILogger<WindowsNetworkMetricsProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<double> GetNetworkIoBytesPerSecondAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_simulatedNetworkIo);
        }
    }
}
