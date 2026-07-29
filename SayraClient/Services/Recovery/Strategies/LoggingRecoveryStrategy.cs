using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class LoggingRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LoggingRecoveryStrategy> _logger;

        public LoggingRecoveryStrategy(IServiceProvider serviceProvider, ILogger<LoggingRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.RestartBackgroundServices; // Or EscalateToAdmin

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Logging Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var auditService = _serviceProvider.GetService<IAuditService>();
            if (auditService != null)
            {
                bool integrityOk = await auditService.VerifyAuditChainIntegrityAsync(cancellationToken);
                _logger.LogInformation("Audit chain integrity verified. Integrity OK: {Result}", integrityOk);
                return true;
            }
            _logger.LogWarning("IAuditService not registered.");
            return true;
        }
    }
}
