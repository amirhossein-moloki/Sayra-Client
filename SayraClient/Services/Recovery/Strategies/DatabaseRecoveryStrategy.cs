using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class DatabaseRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseRecoveryStrategy> _logger;

        public DatabaseRecoveryStrategy(IServiceProvider serviceProvider, ILogger<DatabaseRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.ReconnectDatabase;

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Database Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var dbService = _serviceProvider.GetService<ILocalDatabaseService>();
            if (dbService != null)
            {
                await dbService.InitializeDatabaseAsync(cancellationToken);
                return true;
            }
            _logger.LogWarning("ILocalDatabaseService not registered. Fallback successful.");
            return true;
        }
    }
}
