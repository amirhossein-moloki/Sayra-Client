using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SayraClient.Services;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class SynchronizationRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SynchronizationRecoveryStrategy> _logger;

        public SynchronizationRecoveryStrategy(IServiceProvider serviceProvider, ILogger<SynchronizationRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.ReloadConfiguration; // Can map to ReloadConfiguration or RestartBackgroundServices

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Synchronization Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var syncService = _serviceProvider.GetService<IWorkstationSyncService>();
            if (syncService != null)
            {
                var delta = await syncService.CompareLocalAndServerAsync(cancellationToken);
                _logger.LogInformation("CompareLocalAndServerAsync complete. CalculatedAt: {CalculatedAt}", delta.CalculatedAt);
                return true;
            }
            _logger.LogWarning("IWorkstationSyncService not registered.");
            return true;
        }
    }
}
