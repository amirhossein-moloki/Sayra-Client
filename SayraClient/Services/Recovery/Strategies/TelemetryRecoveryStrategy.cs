using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class TelemetryRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TelemetryRecoveryStrategy> _logger;

        public TelemetryRecoveryStrategy(IServiceProvider serviceProvider, ILogger<TelemetryRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.EscalateToAdmin;

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Telemetry Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var liveTelem = _serviceProvider.GetService<ILiveTelemetryService>();
            if (liveTelem != null)
            {
                await liveTelem.CaptureSnapshotAsync(cancellationToken);
                _logger.LogInformation("Telemetry snapshot captured successfully.");
                return true;
            }
            _logger.LogWarning("ILiveTelemetryService not registered.");
            return true;
        }
    }
}
