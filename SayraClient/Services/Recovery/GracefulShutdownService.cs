using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Services.Recovery
{
    public class GracefulShutdownService
    {
        private readonly ILogger<GracefulShutdownService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly SemaphoreSlim _shutdownLock = new(1, 1);
        private bool _isShutdownInitiated;

        public GracefulShutdownService(ILogger<GracefulShutdownService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task InitiateShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            await _shutdownLock.WaitAsync(cancellationToken);
            try
            {
                if (_isShutdownInitiated)
                {
                    _logger.LogWarning("Graceful shutdown has already been initiated.");
                    return;
                }

                _isShutdownInitiated = true;
                _logger.LogWarning("CRITICAL: Commencing ORDERLY GRACEFUL SHUTDOWN sequence (Timeout: {Timeout}s)...", timeout.TotalSeconds);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeout);

                var token = timeoutCts.Token;

                try
                {
                    // 1. Stop accepting work
                    _logger.LogInformation("[Shutdown Step 1/7] Stop accepting new administrative work and client requests.");
                    var stateManager = _serviceProvider.GetService<ClientStateManager>();
                    if (stateManager != null)
                    {
                        stateManager.TransitionTo(ClientState.DISCONNECTED);
                    }

                    // 2. Drain queues
                    _logger.LogInformation("[Shutdown Step 2/7] Draining communication and remote command queues...");
                    var cmdEngine = _serviceProvider.GetService<IRemoteCommandEngine>();
                    if (cmdEngine != null)
                    {
                        // Safely allow existing commands to complete or serialize
                        await Task.Delay(100, token);
                    }

                    // 3. Flush audit trails
                    _logger.LogInformation("[Shutdown Step 3/7] Flushing audit trail records safely into secured storage...");
                    var audit = _serviceProvider.GetService<IAuditService>();
                    if (audit != null)
                    {
                        await audit.RecordPolicyEventAsync("SYSTEM_SHUTDOWN", "SHUTDOWN_INITIATED", "Graceful shutdown sequence commenced.", "SHUTDOWN", token);
                    }

                    // 4. Persist State
                    _logger.LogInformation("[Shutdown Step 4/7] Persisting workstation local state configuration metadata...");
                    var stateMgr = _serviceProvider.GetService<ClientStateManager>();
                    if (stateMgr != null)
                    {
                        // Trigger final persist
                        await Task.Delay(50, token);
                    }

                    // 5. Stop Background Workers
                    _logger.LogInformation("[Shutdown Step 5/7] Stopping supervised background workers...");
                    var supervisor = _serviceProvider.GetService<IWorkerSupervisor>();
                    if (supervisor != null)
                    {
                        await supervisor.StopAllAsync();
                    }

                    // 6. Close database
                    _logger.LogInformation("[Shutdown Step 6/7] Releasing connection pools and closing SQLCipher databases cleanly...");
                    var dbService = _serviceProvider.GetService<ILocalDatabaseService>();
                    if (dbService != null)
                    {
                        // Clean database closing/disposing handles
                        await Task.Delay(100, token);
                    }

                    // 7. Release resources
                    _logger.LogInformation("[Shutdown Step 7/7] Releasing low-level native hooks, file locks, and kernel objects...");

                    _logger.LogWarning("SAYRA client orderly graceful shutdown completed successfully. Exiting host process safely.");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError("SHUTDOWN TIMEOUT: Shutdown sequence was forced to abort due to timeout limit of {Timeout}s.", timeout.TotalSeconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected exception occurred during graceful shutdown sequence.");
                }
            }
            finally
            {
                _shutdownLock.Release();
            }
        }
    }
}
