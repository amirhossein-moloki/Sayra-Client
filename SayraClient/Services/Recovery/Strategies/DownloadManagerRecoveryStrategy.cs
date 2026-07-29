using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery.Strategies
{
    public class DownloadManagerRecoveryStrategy : IRecoveryActionStrategy
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DownloadManagerRecoveryStrategy> _logger;

        public DownloadManagerRecoveryStrategy(IServiceProvider serviceProvider, ILogger<DownloadManagerRecoveryStrategy> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RecoveryActionType ActionType => RecoveryActionType.RestartDownloads;

        public async Task<bool> ExecuteAsync(string subsystemName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing Download Manager Recovery Strategy for Subsystem: {Subsystem}", subsystemName);
            var downloadManager = _serviceProvider.GetService<IAdDownloadManager>();
            if (downloadManager != null)
            {
                await downloadManager.CleanupOrphanDownloadsAsync(cancellationToken);
                _logger.LogInformation("Cleaned and resumed download queue successfully.");
                return true;
            }
            _logger.LogWarning("IAdDownloadManager not registered.");
            return true;
        }
    }
}
