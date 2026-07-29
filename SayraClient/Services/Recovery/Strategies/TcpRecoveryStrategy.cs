using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class TcpRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TcpRecoveryStrategy> _logger;

        public TcpRecoveryStrategy(IServiceProvider serviceProvider, ILogger<TcpRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.ReconnectTcp;

        public Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing TCP Connections Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var tcpManager = _serviceProvider.GetService<TcpClientManager>();
            if (tcpManager != null)
            {
                tcpManager.Disconnect();
                _logger.LogInformation("TCP connection reset complete.");
                return Task.FromResult(true);
            }
            return Task.FromResult(true);
        }
    }
}
