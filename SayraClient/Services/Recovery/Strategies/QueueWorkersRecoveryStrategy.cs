using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class QueueWorkersRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<QueueWorkersRecoveryStrategy> _logger;

        public QueueWorkersRecoveryStrategy(IServiceProvider serviceProvider, ILogger<QueueWorkersRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.RestartQueueWorkers;

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Queue Workers Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var supervisor = _serviceProvider.GetService<IWorkerSupervisor>();
            if (supervisor != null)
            {
                await supervisor.RestartWorkerAsync("QueueProcessorWorker");
                await supervisor.RestartWorkerAsync("QueueHealthWorker");
                _logger.LogInformation("Queue workers restart sequence triggered.");
                return true;
            }
            return true;
        }
    }
}
