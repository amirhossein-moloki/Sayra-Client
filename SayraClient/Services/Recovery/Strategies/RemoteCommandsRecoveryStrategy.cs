using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class RemoteCommandsRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RemoteCommandsRecoveryStrategy> _logger;

        public RemoteCommandsRecoveryStrategy(IServiceProvider serviceProvider, ILogger<RemoteCommandsRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.RestartBackgroundServices;

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Remote Commands Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var supervisor = _serviceProvider.GetService<IWorkerSupervisor>();
            if (supervisor != null)
            {
                await supervisor.RestartWorkerAsync("RemoteCommandEngine");
                _logger.LogInformation("RemoteCommandEngine worker restarted successfully.");
                return true;
            }
            return true;
        }
    }
}
