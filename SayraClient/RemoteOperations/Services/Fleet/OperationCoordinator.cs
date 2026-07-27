using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.RemoteOperations.Services.Fleet
{
    public class OperationCoordinator
    {
        private readonly ILogger<OperationCoordinator> _logger;
        private readonly ConcurrentDictionary<string, string> _activeOperations = new();
        private readonly object _lock = new();

        public OperationCoordinator(ILogger<OperationCoordinator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> AcquireLockAsync(string resourceKey, string operationType, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Attempting to acquire coordination lock for resource '{Resource}' and operation '{Type}'...", resourceKey, operationType);

            lock (_lock)
            {
                if (_activeOperations.TryGetValue(resourceKey, out var activeOp))
                {
                    _logger.LogWarning("Coordination Lock Denied: Resource '{Resource}' has active operation '{Active}' in progress. Cannot run conflicting '{New}'.",
                        resourceKey, activeOp, operationType);
                    return false;
                }

                _activeOperations[resourceKey] = operationType;
                return true;
            }
        }

        public Task ReleaseLockAsync(string resourceKey)
        {
            _logger.LogInformation("Releasing coordination lock for resource '{Resource}'...", resourceKey);
            _activeOperations.TryRemove(resourceKey, out _);
            return Task.CompletedTask;
        }

        public bool IsExecuting(string resourceKey)
        {
            return _activeOperations.ContainsKey(resourceKey);
        }
    }
}
