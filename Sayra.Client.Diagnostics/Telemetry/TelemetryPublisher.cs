using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Diagnostics.Telemetry
{
    public class TelemetryPublisher : ITelemetryPublisher
    {
        private readonly ILogger<TelemetryPublisher> _logger;

        public TelemetryPublisher(ILogger<TelemetryPublisher> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task PublishAsync(LiveTelemetryData data, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Publishing telemetry payload (CPU: {Cpu}%)", data.CpuUsagePercent);
            return Task.CompletedTask;
        }
    }
}
