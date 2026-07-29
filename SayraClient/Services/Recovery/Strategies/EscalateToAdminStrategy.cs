using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class EscalateToAdminStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EscalateToAdminStrategy> _logger;

        public EscalateToAdminStrategy(IServiceProvider serviceProvider, ILogger<EscalateToAdminStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.EscalateToAdmin;

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogWarning("Escalating critical failure in subsystem '{SubsystemName}' to remote administration.", subsystemName);
            var alertManager = _serviceProvider.GetService<IAlertManager>();
            if (alertManager != null)
            {
                await alertManager.ProcessStatusAsync("LOCAL_PC", "CRITICAL_SUBSYSTEM_FAILED", $"Subsystem {subsystemName} entered a critical, non-recoverable state.", cancellationToken);
                _logger.LogInformation("Escalation alert sent successfully.");
                return true;
            }
            _logger.LogWarning("IAlertManager not registered. Escalation alert logged only.");
            return true;
        }
    }
}
