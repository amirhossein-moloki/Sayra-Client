using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SayraClient.Services;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class ShutdownWorkstationStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ShutdownWorkstationStrategy> _logger;

        public ShutdownWorkstationStrategy(IServiceProvider serviceProvider, ILogger<ShutdownWorkstationStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.ShutdownWorkstation;

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogWarning("Executing Shutdown Workstation Strategy triggered by subsystem '{SubsystemName}'.", subsystemName);
            var powerService = _serviceProvider.GetService<IPowerManagementService>();
            if (powerService != null)
            {
                await powerService.ShutdownAsync(cancellationToken);
                _logger.LogInformation("Shutdown sequence initiated.");
                return true;
            }
            _logger.LogWarning("IPowerManagementService not registered.");
            return true;
        }
    }
}
