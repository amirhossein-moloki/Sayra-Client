using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class NetworkRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NetworkRecoveryStrategy> _logger;

        public NetworkRecoveryStrategy(IServiceProvider serviceProvider, ILogger<NetworkRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.ReconnectTcp;

        public Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Network Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var tcpManager = _serviceProvider.GetService<TcpClientManager>();
            if (tcpManager != null)
            {
                tcpManager.Disconnect();
                _logger.LogInformation("Successfully triggered TCP client disconnect to force a reconnect sequence.");
                return Task.FromResult(true);
            }
            _logger.LogWarning("TcpClientManager not registered.");
            return Task.FromResult(true);
        }
    }
}
