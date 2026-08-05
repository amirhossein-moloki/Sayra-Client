using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Fleet;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.BulkOperations
{
    /// <summary>
    /// Thread-safe implementation of IBulkOperationRepository that persists state to disk using lightweight JSON serialization
    /// to seamlessly recover operation definitions and execution states across application restarts.
    /// Utilizes a high-throughput async debouncing background worker to prevent unscalable disk writing bottlenecks
    /// during multi-thousand machine parallel operations.
    /// </summary>
    public class BulkOperationRepository : IBulkOperationRepository, IDisposable
    {
        private readonly string _persistenceFilePath;
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private readonly CancellationTokenSource _workerCts = new();
        private readonly AutoResetEvent _saveRequestEvent = new(false);

        private readonly ConcurrentDictionary<string, BulkOperation> _operations = new();
        private readonly ConcurrentDictionary<string, List<BulkOperationTarget>> _targets = new();
        private readonly ConcurrentDictionary<string, BulkOperationResult> _results = new();
        private readonly ConcurrentDictionary<string, List<BulkOperationFailure>> _failures = new();
        private readonly ConcurrentDictionary<string, BulkOperationProgress> _progress = new();
        private readonly ConcurrentDictionary<string, List<BulkOperationProgress>> _progressHistory = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, BulkOperationExecution>> _executions = new();
        private readonly ConcurrentDictionary<string, BulkOperationPolicy> _policies = new();

        private int _isDirty = 0; // Thread-safe flag (0 = clean, 1 = dirty)

        /// <summary>
        /// Initializes a new instance of BulkOperationRepository.
        /// </summary>
        public BulkOperationRepository()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var sayraDir = Path.Combine(appData, "Sayra", "BulkOperations");
            Directory.CreateDirectory(sayraDir);
            _persistenceFilePath = Path.Combine(sayraDir, "bulk_operations_state.json");

            LoadStateFromDisk();
            StartBackgroundPersistenceWorker();
        }

        /// <summary>
        /// Explicit constructor specifying custom persistence file path (extremely useful for isolated unit/integration tests).
        /// </summary>
        public BulkOperationRepository(string persistenceFilePath)
        {
            var directory = Path.GetDirectoryName(persistenceFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            _persistenceFilePath = persistenceFilePath;
            LoadStateFromDisk();
            StartBackgroundPersistenceWorker();
        }

        private void LoadStateFromDisk()
        {
            try
            {
                if (!File.Exists(_persistenceFilePath)) return;

                var json = File.ReadAllText(_persistenceFilePath);
                var stateDto = JsonSerializer.Deserialize<RepositoryStateDto>(json);
                if (stateDto == null) return;

                if (stateDto.Operations != null)
                {
                    foreach (var op in stateDto.Operations)
                    {
                        _operations[op.BulkOperationId] = op;
                    }
                }

                if (stateDto.Targets != null)
                {
                    foreach (var kvp in stateDto.Targets)
                    {
                        _targets[kvp.Key] = kvp.Value;
                    }
                }

                if (stateDto.Results != null)
                {
                    foreach (var res in stateDto.Results)
                    {
                        _results[res.BulkOperationId] = res;
                    }
                }

                if (stateDto.Failures != null)
                {
                    foreach (var kvp in stateDto.Failures)
                    {
                        _failures[kvp.Key] = kvp.Value;
                    }
                }

                if (stateDto.Progress != null)
                {
                    foreach (var kvp in stateDto.Progress)
                    {
                        _progress[kvp.Key] = kvp.Value;
                    }
                }

                if (stateDto.ProgressHistory != null)
                {
                    foreach (var kvp in stateDto.ProgressHistory)
                    {
                        _progressHistory[kvp.Key] = kvp.Value;
                    }
                }

                if (stateDto.Executions != null)
                {
                    foreach (var kvp in stateDto.Executions)
                    {
                        var dict = new ConcurrentDictionary<string, BulkOperationExecution>();
                        foreach (var exec in kvp.Value)
                        {
                            dict[exec.MachineId] = exec;
                        }
                        _executions[kvp.Key] = dict;
                    }
                }

                if (stateDto.Policies != null)
                {
                    foreach (var kvp in stateDto.Policies)
                    {
                        _policies[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception)
            {
                // Fault-tolerant: if backup file is corrupted, start fresh
            }
        }

        private void StartBackgroundPersistenceWorker()
        {
            Task.Run(async () =>
            {
                var token = _workerCts.Token;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Wait for a save request or timeout of 1.5 seconds
                        _saveRequestEvent.WaitOne(1500);

                        if (Interlocked.CompareExchange(ref _isDirty, 0, 1) == 1)
                        {
                            await SaveStateToDiskInternalAsync();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        // Safely swallow background errors to preserve execution stability
                    }
                }
            });
        }

        private void QueueDiskWrite()
        {
            Interlocked.Exchange(ref _isDirty, 1);
            _saveRequestEvent.Set();
        }

        private async Task SaveStateToDiskInternalAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                var stateDto = new RepositoryStateDto
                {
                    Operations = _operations.Values.ToList(),
                    Targets = _targets.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Results = _results.Values.ToList(),
                    Failures = _failures.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Progress = _progress.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    ProgressHistory = _progressHistory.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Executions = _executions.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Values.ToList()
                    ),
                    Policies = _policies.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                };

                var json = JsonSerializer.Serialize(stateDto, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_persistenceFilePath, json);
            }
            catch (Exception)
            {
                // Isolate disk write failures to preserve active task reliability
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <inheritdoc />
        public Task SaveOperationAsync(BulkOperation operation, CancellationToken ct = default)
        {
            _operations[operation.BulkOperationId] = operation;
            QueueDiskWrite();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<BulkOperation?> GetOperationAsync(string bulkOperationId, CancellationToken ct = default)
        {
            _operations.TryGetValue(bulkOperationId, out var operation);
            return Task.FromResult(operation);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<BulkOperation>> GetAllOperationsAsync(CancellationToken ct = default)
        {
            IReadOnlyList<BulkOperation> list = _operations.Values.ToList();
            return Task.FromResult(list);
        }

        /// <inheritdoc />
        public Task SaveTargetsAsync(string bulkOperationId, IEnumerable<BulkOperationTarget> targets, CancellationToken ct = default)
        {
            _targets[bulkOperationId] = targets.ToList();
            QueueDiskWrite();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<BulkOperationTarget>> GetTargetsAsync(string bulkOperationId, CancellationToken ct = default)
        {
            if (_targets.TryGetValue(bulkOperationId, out var list))
            {
                IReadOnlyList<BulkOperationTarget> readonlyList = list;
                return Task.FromResult(readonlyList);
            }
            return Task.FromResult<IReadOnlyList<BulkOperationTarget>>(Array.Empty<BulkOperationTarget>());
        }

        /// <inheritdoc />
        public Task SaveResultAsync(BulkOperationResult result, CancellationToken ct = default)
        {
            _results[result.BulkOperationId] = result;
            QueueDiskWrite();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<BulkOperationResult?> GetResultAsync(string bulkOperationId, CancellationToken ct = default)
        {
            _results.TryGetValue(bulkOperationId, out var result);
            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task SaveFailureAsync(string bulkOperationId, BulkOperationFailure failure, CancellationToken ct = default)
        {
            _failures.AddOrUpdate(bulkOperationId,
                _ => new List<BulkOperationFailure> { failure },
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(failure);
                    }
                    return list;
                });
            QueueDiskWrite();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<BulkOperationFailure>> GetFailuresAsync(string bulkOperationId, CancellationToken ct = default)
        {
            if (_failures.TryGetValue(bulkOperationId, out var list))
            {
                lock (list)
                {
                    IReadOnlyList<BulkOperationFailure> copy = list.ToList();
                    return Task.FromResult(copy);
                }
            }
            return Task.FromResult<IReadOnlyList<BulkOperationFailure>>(Array.Empty<BulkOperationFailure>());
        }

        /// <inheritdoc />
        public Task SaveProgressAsync(string bulkOperationId, BulkOperationProgress progress, CancellationToken ct = default)
        {
            _progress[bulkOperationId] = progress;

            _progressHistory.AddOrUpdate(bulkOperationId,
                _ => new List<BulkOperationProgress> { progress },
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(progress);
                    }
                    return list;
                });

            QueueDiskWrite();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<BulkOperationProgress?> GetProgressAsync(string bulkOperationId, CancellationToken ct = default)
        {
            _progress.TryGetValue(bulkOperationId, out var progress);
            return Task.FromResult(progress);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<BulkOperationProgress>> GetProgressHistoryAsync(string bulkOperationId, CancellationToken ct = default)
        {
            if (_progressHistory.TryGetValue(bulkOperationId, out var list))
            {
                lock (list)
                {
                    IReadOnlyList<BulkOperationProgress> copy = list.ToList();
                    return Task.FromResult(copy);
                }
            }
            return Task.FromResult<IReadOnlyList<BulkOperationProgress>>(Array.Empty<BulkOperationProgress>());
        }

        /// <inheritdoc />
        public Task SaveExecutionStateAsync(string bulkOperationId, BulkOperationExecution execution, CancellationToken ct = default)
        {
            var dict = _executions.GetOrAdd(bulkOperationId, _ => new ConcurrentDictionary<string, BulkOperationExecution>());
            dict[execution.MachineId] = execution;
            QueueDiskWrite();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<BulkOperationExecution>> GetExecutionsAsync(string bulkOperationId, CancellationToken ct = default)
        {
            if (_executions.TryGetValue(bulkOperationId, out var dict))
            {
                IReadOnlyList<BulkOperationExecution> list = dict.Values.ToList();
                return Task.FromResult(list);
            }
            return Task.FromResult<IReadOnlyList<BulkOperationExecution>>(Array.Empty<BulkOperationExecution>());
        }

        /// <inheritdoc />
        public Task SavePolicyAsync(string bulkOperationId, BulkOperationPolicy policy, CancellationToken ct = default)
        {
            _policies[bulkOperationId] = policy;
            QueueDiskWrite();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<BulkOperationPolicy?> GetPolicyAsync(string bulkOperationId, CancellationToken ct = default)
        {
            _policies.TryGetValue(bulkOperationId, out var policy);
            return Task.FromResult<BulkOperationPolicy?>(policy);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _workerCts.Cancel();
            _workerCts.Dispose();
            _saveRequestEvent.Dispose();
            _fileLock.Dispose();
        }

        /// <summary>
        /// Inner class representing backup payload structure.
        /// </summary>
        private class RepositoryStateDto
        {
            public List<BulkOperation>? Operations { get; set; }
            public Dictionary<string, List<BulkOperationTarget>>? Targets { get; set; }
            public List<BulkOperationResult>? Results { get; set; }
            public Dictionary<string, List<BulkOperationFailure>>? Failures { get; set; }
            public Dictionary<string, BulkOperationProgress>? Progress { get; set; }
            public Dictionary<string, List<BulkOperationProgress>>? ProgressHistory { get; set; }
            public Dictionary<string, List<BulkOperationExecution>>? Executions { get; set; }
            public Dictionary<string, BulkOperationPolicy>? Policies { get; set; }
        }
    }
}
