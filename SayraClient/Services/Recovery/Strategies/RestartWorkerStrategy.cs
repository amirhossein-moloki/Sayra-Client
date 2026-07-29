using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class RestartWorkerStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RestartWorkerStrategy> _logger;

        public RestartWorkerStrategy(IServiceProvider serviceProvider, ILogger<RestartWorkerStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.RestartWorker;

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Restart Worker Strategy for Subsystem: {Subsystem}", subsystemName);
            var supervisor = _serviceProvider.GetService<IWorkerSupervisor>();
            if (supervisor != null)
            {
                await supervisor.RestartWorkerAsync(subsystemName);
                _logger.LogInformation("Successfully restarted worker '{WorkerName}' via worker supervisor.", subsystemName);
                return true;
            }
            return true;
        }
    }
}
