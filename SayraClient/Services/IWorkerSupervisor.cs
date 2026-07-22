using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SayraClient.Services;

/// <summary>
/// Supervises and manages the lifecycle, dependencies, and restart policies of background workers.
/// </summary>
public interface IWorkerSupervisor
{
    /// <summary>
    /// Registers a background worker with its execution delegate and dependencies.
    /// </summary>
    void RegisterWorker(string name, Func<CancellationToken, Task> executeTask, IEnumerable<string>? dependencies = null);

    /// <summary>
    /// Starts all registered workers in the correct dependency order.
    /// </summary>
    Task StartAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gracefully stops all workers in reverse dependency order.
    /// </summary>
    Task StopAllAsync();

    /// <summary>
    /// Restarts a specific worker manually (e.g. for recovery).
    /// </summary>
    Task RestartWorkerAsync(string name);
}
