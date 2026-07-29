using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class NotificationQueueRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NotificationQueueRecoveryStrategy> _logger;

        public NotificationQueueRecoveryStrategy(IServiceProvider serviceProvider, ILogger<NotificationQueueRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.RestartQueueWorkers;

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Notification Queue Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var supervisor = _serviceProvider.GetService<IWorkerSupervisor>();
            if (supervisor != null)
            {
                await supervisor.RestartWorkerAsync("QueueProcessorWorker");
                _logger.LogInformation("Successfully restarted QueueProcessorWorker for notification processing.");
                return true;
            }
            return true;
        }
    }
}
