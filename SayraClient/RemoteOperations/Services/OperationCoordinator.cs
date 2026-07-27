using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.RemoteOperations.Services
{
    public class OperationCoordinator
    {
        private readonly ILogger<OperationCoordinator> _logger;
        private readonly ConcurrentDictionary<string, bool> _activeOperations = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        // Conflict matrix: Key: operation type, Value: list of other operation types that conflict with it
        private readonly Dictionary<string, List<string>> _conflicts = new(StringComparer.OrdinalIgnoreCase);

        public OperationCoordinator(ILogger<OperationCoordinator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Define conflicts
            // Bulk Operations conflict with Remote Commands and Policy Distributions
            _conflicts["BULK_OPERATION"] = new List<string> { "REMOTE_COMMAND", "POLICY_DISTRIBUTION" };
            _conflicts["REMOTE_COMMAND"] = new List<string> { "BULK_OPERATION" };
            _conflicts["POLICY_DISTRIBUTION"] = new List<string> { "BULK_OPERATION", "POLICY_DISTRIBUTION" };
            _conflicts["TELEMETRY_REQUEST"] = new List<string> { "TELEMETRY_REQUEST" };
            _conflicts["DIAGNOSTICS_REQUEST"] = new List<string> { "DIAGNOSTICS_REQUEST" };
        }

        public async Task<IDisposable?> TryAcquireLockAsync(string operationType, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(operationType)) throw new ArgumentException("Operation type cannot be null or empty", nameof(operationType));

            await _lock.WaitAsync(ct);
            try
            {
                string opKey = operationType.ToUpperInvariant();

                // 1. Check if the exact operation type is already executing (prevent duplicate execution)
                if (_activeOperations.ContainsKey(opKey))
                {
                    _logger.LogWarning("Operation of type '{OperationType}' is already executing. Prevention of duplicate execution triggered.", opKey);
                    return null;
                }

                // 2. Check conflict rules
                if (_conflicts.TryGetValue(opKey, out var conflictingTypes))
                {
                    foreach (var conflict in conflictingTypes)
                    {
                        if (_activeOperations.ContainsKey(conflict.ToUpperInvariant()))
                        {
                            _logger.LogWarning("Operation of type '{OperationType}' cannot execute due to conflict with active '{Conflict}'", opKey, conflict);
                            return null;
                        }
                    }
                }

                // 3. Acquire lock
                _activeOperations.TryAdd(opKey, true);
                _logger.LogInformation("Lock acquired successfully for operation type '{OperationType}'", opKey);

                return new OperationLock(this, opKey);
            }
            finally
            {
                _lock.Release();
            }
        }

        public void ReleaseLock(string operationType)
        {
            if (string.IsNullOrEmpty(operationType)) return;

            string opKey = operationType.ToUpperInvariant();
            if (_activeOperations.TryRemove(opKey, out _))
            {
                _logger.LogInformation("Lock released for operation type '{OperationType}'", opKey);
            }
        }

        private class OperationLock : IDisposable
        {
            private readonly OperationCoordinator _coordinator;
            private readonly string _operationType;
            private int _disposed;

            public OperationLock(OperationCoordinator coordinator, string operationType)
            {
                _coordinator = coordinator;
                _operationType = operationType;
            }

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                {
                    _coordinator.ReleaseLock(_operationType);
                }
            }
        }
    }
}
