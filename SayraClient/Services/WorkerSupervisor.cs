using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class SupervisedWorker
{
    public string Name { get; init; } = string.Empty;
    public HashSet<string> Dependencies { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Func<CancellationToken, Task> ExecuteTask { get; init; } = null!;

    public CancellationTokenSource? Cts { get; set; }
    public Task? RunningTask { get; set; }
    public int CrashCount { get; set; }
    public ServiceHealthState State { get; set; } = ServiceHealthState.Stopped;
    public DateTime? LastStartedAt { get; set; }
    public object LockObject { get; } = new();
}

public class WorkerSupervisor : IWorkerSupervisor, IDisposable
{
    private readonly ILogger<WorkerSupervisor> _logger;
    private readonly IServiceHealthMonitor _healthMonitor;
    private readonly ConcurrentDictionary<string, SupervisedWorker> _workers = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _supervisorCts = new();
    private bool _isStopped;
    private readonly int _maxCrashCount = 5;
    private readonly TimeSpan _maxBackoff = TimeSpan.FromSeconds(30);

    public WorkerSupervisor(ILogger<WorkerSupervisor> logger, IServiceHealthMonitor healthMonitor)
    {
        _logger = logger;
        _healthMonitor = healthMonitor;
    }

    public void RegisterWorker(string name, Func<CancellationToken, Task> executeTask, IEnumerable<string>? dependencies = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Worker name cannot be empty.", nameof(name));
        if (executeTask == null) throw new ArgumentNullException(nameof(executeTask));

        var worker = new SupervisedWorker
        {
            Name = name,
            ExecuteTask = executeTask,
            Dependencies = new HashSet<string>(dependencies ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase)
        };

        if (!_workers.TryAdd(name, worker))
        {
            throw new InvalidOperationException($"Worker '{name}' is already registered.");
        }

        _logger.LogInformation("Worker '{WorkerName}' registered with dependencies: [{Dependencies}].", name, string.Join(", ", worker.Dependencies));
        _healthMonitor.ReportState(name, ServiceHealthState.Stopped, "Worker registered but not started.");
    }

    public async Task StartAllAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Supervisor starting all registered workers...");
        _isStopped = false;

        var orderedWorkers = GetTopologicallySortedWorkers();
        foreach (var worker in orderedWorkers)
        {
            if (cancellationToken.IsCancellationRequested) break;
            await StartWorkerInternalAsync(worker);
        }
    }

    public async Task StopAllAsync()
    {
        _logger.LogInformation("Supervisor stopping all registered workers...");
        _isStopped = true;
        _supervisorCts.Cancel();

        var orderedWorkers = GetTopologicallySortedWorkers();
        // Stop in reverse order
        orderedWorkers.Reverse();

        foreach (var worker in orderedWorkers)
        {
            await StopWorkerInternalAsync(worker);
        }
    }

    public async Task RestartWorkerAsync(string name)
    {
        if (!_workers.TryGetValue(name, out var worker))
        {
            _logger.LogWarning("Cannot restart unregistered worker '{WorkerName}'.", name);
            return;
        }

        _logger.LogWarning("Manual restart requested for worker '{WorkerName}'.", name);
        await StopWorkerInternalAsync(worker);

        lock (worker.LockObject)
        {
            worker.CrashCount = 0; // Reset crash count on manual restart
        }

        await StartWorkerInternalAsync(worker);
    }

    private async Task StartWorkerInternalAsync(SupervisedWorker worker)
    {
        lock (worker.LockObject)
        {
            if (worker.State == ServiceHealthState.Healthy || worker.State == ServiceHealthState.Starting)
            {
                _logger.LogDebug("Worker '{WorkerName}' is already in state {State}, skipping start.", worker.Name, worker.State);
                return;
            }

            _logger.LogInformation("Starting worker '{WorkerName}'...", worker.Name);
            worker.State = ServiceHealthState.Starting;
            _healthMonitor.ReportState(worker.Name, ServiceHealthState.Starting, "Worker starting...");

            worker.Cts = CancellationTokenSource.CreateLinkedTokenSource(_supervisorCts.Token);
            worker.LastStartedAt = DateTime.UtcNow;

            var token = worker.Cts.Token;
            worker.RunningTask = Task.Run(async () =>
            {
                try
                {
                    _healthMonitor.ReportState(worker.Name, ServiceHealthState.Healthy, "Worker execution started.");
                    lock (worker.LockObject)
                    {
                        worker.State = ServiceHealthState.Healthy;
                    }

                    // Execute worker task
                    await worker.ExecuteTask(token);

                    _logger.LogInformation("Worker '{WorkerName}' completed its task successfully.", worker.Name);
                    _healthMonitor.ReportState(worker.Name, ServiceHealthState.Stopped, "Worker completed execution.");
                    lock (worker.LockObject)
                    {
                        worker.State = ServiceHealthState.Stopped;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    _logger.LogInformation("Worker '{WorkerName}' was cancelled gracefully.", worker.Name);
                    _healthMonitor.ReportState(worker.Name, ServiceHealthState.Stopped, "Worker stopped gracefully.");
                    lock (worker.LockObject)
                    {
                        worker.State = ServiceHealthState.Stopped;
                    }
                }
                catch (Exception ex)
                {
                    await HandleWorkerCrashAsync(worker, ex);
                }
            }, token);
        }

        await Task.CompletedTask;
    }

    private async Task StopWorkerInternalAsync(SupervisedWorker worker)
    {
        CancellationTokenSource? ctsToCancel;
        Task? taskToWait;

        lock (worker.LockObject)
        {
            if (worker.State == ServiceHealthState.Stopped) return;

            _logger.LogInformation("Stopping worker '{WorkerName}' gracefully...", worker.Name);
            _healthMonitor.ReportState(worker.Name, ServiceHealthState.Stopped, "Worker stopping...");
            worker.State = ServiceHealthState.Stopped;

            ctsToCancel = worker.Cts;
            taskToWait = worker.RunningTask;

            worker.Cts = null;
            worker.RunningTask = null;
        }

        if (ctsToCancel != null)
        {
            try
            {
                ctsToCancel.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cancelling token for worker '{WorkerName}'.", worker.Name);
            }
            finally
            {
                ctsToCancel.Dispose();
            }
        }

        if (taskToWait != null)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await Task.WhenAny(taskToWait, Task.Delay(Timeout.Infinite, timeoutCts.Token));

                if (!taskToWait.IsCompleted)
                {
                    _logger.LogWarning("Worker '{WorkerName}' did not stop within timeout. Forcing termination.", worker.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for worker '{WorkerName}' task to complete.", worker.Name);
            }
        }
    }

    private async Task HandleWorkerCrashAsync(SupervisedWorker worker, Exception exception)
    {
        int crashCount;
        lock (worker.LockObject)
        {
            worker.CrashCount++;
            crashCount = worker.CrashCount;
            worker.State = ServiceHealthState.Failed;
        }

        _logger.LogError(exception, "CRITICAL: Worker '{WorkerName}' crashed! Crash count: {CrashCount}/{MaxCrashCount}", worker.Name, crashCount, _maxCrashCount);
        _healthMonitor.ReportFailure(worker.Name, exception, $"Worker crashed. Crash count: {crashCount}/{_maxCrashCount}");

        if (_isStopped || _supervisorCts.IsCancellationRequested)
        {
            _logger.LogInformation("Supervisor is stopping. Skipping restart for worker '{WorkerName}'.", worker.Name);
            return;
        }

        if (crashCount >= _maxCrashCount)
        {
            _logger.LogCritical("CRITICAL: Worker '{WorkerName}' exceeded maximum crash limit ({MaxCrashCount}). Permanent failure state set.", worker.Name, _maxCrashCount);
            return;
        }

        // Exponential backoff
        double seconds = Math.Min(_maxBackoff.TotalSeconds, Math.Pow(2, crashCount - 1));
        var backoffDelay = TimeSpan.FromSeconds(seconds);

        _logger.LogWarning("Worker '{WorkerName}' will be restarted after exponential backoff of {BackoffSeconds}s.", worker.Name, backoffDelay.TotalSeconds);
        _healthMonitor.ReportState(worker.Name, ServiceHealthState.Recovering, $"Crashed. Restarting in {backoffDelay.TotalSeconds}s...");

        try
        {
            await Task.Delay(backoffDelay, _supervisorCts.Token);
            _logger.LogInformation("Attempting restart of crashed worker '{WorkerName}' (Attempt {CrashCount})...", worker.Name, crashCount);
            await StartWorkerInternalAsync(worker);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Supervisor cancelled during restart backoff for worker '{WorkerName}'.", worker.Name);
        }
    }

    private List<SupervisedWorker> GetTopologicallySortedWorkers()
    {
        var visited = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<SupervisedWorker>();

        foreach (var worker in _workers.Values)
        {
            if (!visited.ContainsKey(worker.Name))
            {
                VisitWorker(worker, visited, stack, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
        }

        return stack;
    }

    private void VisitWorker(SupervisedWorker worker, Dictionary<string, bool> visited, List<SupervisedWorker> stack, HashSet<string> currentPath)
    {
        if (currentPath.Contains(worker.Name))
        {
            throw new InvalidOperationException($"Circular dependency detected: {string.Join(" -> ", currentPath)} -> {worker.Name}");
        }

        currentPath.Add(worker.Name);

        foreach (var depName in worker.Dependencies)
        {
            if (_workers.TryGetValue(depName, out var depWorker))
            {
                if (!visited.TryGetValue(depName, out bool complete) || !complete)
                {
                    VisitWorker(depWorker, visited, stack, currentPath);
                }
            }
            else
            {
                _logger.LogWarning("Worker '{WorkerName}' depends on missing dependency '{DependencyName}'. Continuing anyway.", worker.Name, depName);
            }
        }

        currentPath.Remove(worker.Name);
        visited[worker.Name] = true;
        stack.Add(worker);
    }

    public void Dispose()
    {
        _supervisorCts.Dispose();
    }
}
