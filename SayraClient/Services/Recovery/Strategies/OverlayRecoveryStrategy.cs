using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class OverlayRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OverlayRecoveryStrategy> _logger;

        public OverlayRecoveryStrategy(IServiceProvider serviceProvider, ILogger<OverlayRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.RestartOverlay;

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Overlay Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var overlayManager = _serviceProvider.GetService<IOverlayManager>();
            if (overlayManager != null)
            {
                await overlayManager.ShowAsync();
                _logger.LogInformation("Overlay restart complete via IOverlayManager.");
                return true;
            }
            _logger.LogWarning("IOverlayManager not registered.");
            return true;
        }
    }
}
