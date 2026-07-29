using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Configuration.Synchronization;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class ConfigurationReloadRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConfigurationReloadRecoveryStrategy> _logger;

        public ConfigurationReloadRecoveryStrategy(IServiceProvider serviceProvider, ILogger<ConfigurationReloadRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.ReloadConfiguration;

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Configuration Reload Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var syncService = _serviceProvider.GetService<IConfigurationSynchronizationService>();
            if (syncService != null)
            {
                bool result = await syncService.PullAndApplyAsync(cancellationToken);
                _logger.LogInformation("Configuration sync PullAndApplyAsync completed. Success: {Result}", result);
                return result;
            }
            _logger.LogWarning("IConfigurationSynchronizationService not registered.");
            return true;
        }
    }
}
