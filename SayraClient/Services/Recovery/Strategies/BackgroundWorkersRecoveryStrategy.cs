using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class BackgroundWorkersRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundWorkersRecoveryStrategy> _logger;

        public BackgroundWorkersRecoveryStrategy(IServiceProvider serviceProvider, ILogger<BackgroundWorkersRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.RestartBackgroundServices;

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Background Workers Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var supervisor = _serviceProvider.GetService<IWorkerSupervisor>();
            if (supervisor != null)
            {
                // Restart main worker or subsystem-specific worker
                await supervisor.RestartWorkerAsync("Worker");
                _logger.LogInformation("Main background worker restart triggered.");
                return true;
            }
            return true;
        }
    }
}
