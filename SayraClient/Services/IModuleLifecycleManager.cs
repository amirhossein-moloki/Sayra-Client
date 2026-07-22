using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SayraClient.Services;

/// <summary>
/// States that a module transitions through during its operational lifecycle.
/// </summary>
public enum ModuleState
{
    Registered,
    Initializing,
    Initialized,
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed,
    Disposed
}

/// <summary>
/// Contract for highly decoupled modules that require lifecycle management.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Unique identifier for the module.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Collection of module names that this module depends on.
    /// </summary>
    IReadOnlyCollection<string> Dependencies { get; }

    /// <summary>
    /// Runs initialization operations. Called before starting.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts the module services.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the module services gracefully.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Manages registering, initializing, starting, and stopping of modular services in dependency-sorted order.
/// </summary>
public interface IModuleLifecycleManager
{
    /// <summary>
    /// Event raised when any registered module changes state.
    /// </summary>
    event Action<string, ModuleState, ModuleState>? ModuleStateChanged;

    /// <summary>
    /// Registers a module to be managed.
    /// </summary>
    void RegisterModule(IModule module);

    /// <summary>
    /// Performs dependency-ordered initialization for all registered modules.
    /// </summary>
    Task InitializeAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Performs dependency-ordered startup for all registered modules.
    /// </summary>
    Task StartAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Performs reverse-dependency-ordered shutdown for all registered modules.
    /// </summary>
    Task StopAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves current state of a given module.
    /// </summary>
    ModuleState GetModuleState(string moduleName);

    /// <summary>
    /// Gets snapshot of states for all registered modules.
    /// </summary>
    IReadOnlyDictionary<string, ModuleState> GetAllModuleStates();
}
