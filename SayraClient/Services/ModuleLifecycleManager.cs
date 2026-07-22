using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class ModuleLifecycleManager : IModuleLifecycleManager
{
    private readonly ILogger<ModuleLifecycleManager> _logger;
    private readonly ConcurrentDictionary<string, IModule> _modules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ModuleState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);

    public event Action<string, ModuleState, ModuleState>? ModuleStateChanged;

    public ModuleLifecycleManager(ILogger<ModuleLifecycleManager> logger)
    {
        _logger = logger;
    }

    public void RegisterModule(IModule module)
    {
        if (module == null) throw new ArgumentNullException(nameof(module));
        if (string.IsNullOrWhiteSpace(module.Name)) throw new ArgumentException("Module name cannot be empty.", nameof(module));

        if (!_modules.TryAdd(module.Name, module))
        {
            throw new InvalidOperationException($"Module '{module.Name}' is already registered.");
        }

        _states[module.Name] = ModuleState.Registered;
        _logger.LogInformation("Module '{ModuleName}' registered. Dependencies: [{Dependencies}]", module.Name, string.Join(", ", module.Dependencies));
        NotifyStateChanged(module.Name, ModuleState.Registered, ModuleState.Registered);
    }

    public async Task InitializeAllAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing all registered modules...");
        ValidateDependencies();

        var sortedModules = GetTopologicallySortedModules();
        foreach (var module in sortedModules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await TransitionStateAsync(module.Name, ModuleState.Initializing);

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_defaultTimeout);

                _logger.LogInformation("Initializing module '{ModuleName}'...", module.Name);
                await module.InitializeAsync(timeoutCts.Token);

                await TransitionStateAsync(module.Name, ModuleState.Initialized);
                _logger.LogInformation("Module '{ModuleName}' initialized successfully.", module.Name);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("TIMEOUT: Initialization of module '{ModuleName}' timed out after {Timeout}s.", module.Name, _defaultTimeout.TotalSeconds);
                await TransitionStateAsync(module.Name, ModuleState.Failed);
                throw new TimeoutException($"Initialization of module '{module.Name}' timed out after {_defaultTimeout.TotalSeconds} seconds.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FATAL: Initialization of module '{ModuleName}' failed.", module.Name);
                await TransitionStateAsync(module.Name, ModuleState.Failed);
                throw;
            }
        }
    }

    public async Task StartAllAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting all registered modules...");

        var sortedModules = GetTopologicallySortedModules();
        foreach (var module in sortedModules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentState = GetModuleState(module.Name);
            if (currentState != ModuleState.Initialized)
            {
                throw new InvalidOperationException($"Cannot start module '{module.Name}' because it is in state {currentState} instead of Initialized.");
            }

            await TransitionStateAsync(module.Name, ModuleState.Starting);

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_defaultTimeout);

                _logger.LogInformation("Starting module '{ModuleName}'...", module.Name);
                await module.StartAsync(timeoutCts.Token);

                await TransitionStateAsync(module.Name, ModuleState.Running);
                _logger.LogInformation("Module '{ModuleName}' is now running.", module.Name);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("TIMEOUT: Startup of module '{ModuleName}' timed out after {Timeout}s.", module.Name, _defaultTimeout.TotalSeconds);
                await TransitionStateAsync(module.Name, ModuleState.Failed);
                throw new TimeoutException($"Startup of module '{module.Name}' timed out after {_defaultTimeout.TotalSeconds} seconds.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FATAL: Startup of module '{ModuleName}' failed.", module.Name);
                await TransitionStateAsync(module.Name, ModuleState.Failed);
                throw;
            }
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping all registered modules...");

        var sortedModules = GetTopologicallySortedModules();
        // Stop in reverse order of startup (reverse dependency order)
        sortedModules.Reverse();

        foreach (var module in sortedModules)
        {
            var currentState = GetModuleState(module.Name);
            if (currentState == ModuleState.Stopped || currentState == ModuleState.Disposed)
            {
                continue;
            }

            await TransitionStateAsync(module.Name, ModuleState.Stopping);

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_defaultTimeout);

                _logger.LogInformation("Stopping module '{ModuleName}'...", module.Name);
                await module.StopAsync(timeoutCts.Token);

                await TransitionStateAsync(module.Name, ModuleState.Stopped);
                _logger.LogInformation("Module '{ModuleName}' stopped successfully.", module.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping module '{ModuleName}'. Continuing shutdown of other modules.", module.Name);
                await TransitionStateAsync(module.Name, ModuleState.Failed);
            }
        }

        // Dispose disposable modules
        foreach (var module in sortedModules)
        {
            if (module is IDisposable disposable)
            {
                try
                {
                    _logger.LogInformation("Disposing module '{ModuleName}'...", module.Name);
                    disposable.Dispose();
                    await TransitionStateAsync(module.Name, ModuleState.Disposed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing module '{ModuleName}'.", module.Name);
                }
            }
        }
    }

    public ModuleState GetModuleState(string moduleName)
    {
        return _states.TryGetValue(moduleName, out var state) ? state : ModuleState.Stopped;
    }

    public IReadOnlyDictionary<string, ModuleState> GetAllModuleStates()
    {
        return _states.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    private void ValidateDependencies()
    {
        foreach (var module in _modules.Values)
        {
            foreach (var dep in module.Dependencies)
            {
                if (!_modules.ContainsKey(dep))
                {
                    throw new InvalidOperationException($"Module '{module.Name}' depends on module '{dep}', which is not registered.");
                }
            }
        }
    }

    private List<IModule> GetTopologicallySortedModules()
    {
        var visited = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<IModule>();

        foreach (var module in _modules.Values)
        {
            if (!visited.ContainsKey(module.Name))
            {
                VisitModule(module, visited, stack, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
        }

        return stack;
    }

    private void VisitModule(IModule module, Dictionary<string, bool> visited, List<IModule> stack, HashSet<string> currentPath)
    {
        if (currentPath.Contains(module.Name))
        {
            throw new InvalidOperationException($"Circular dependency detected in module configuration: {string.Join(" -> ", currentPath)} -> {module.Name}");
        }

        currentPath.Add(module.Name);

        foreach (var depName in module.Dependencies)
        {
            if (_modules.TryGetValue(depName, out var depModule))
            {
                if (!visited.TryGetValue(depName, out bool complete) || !complete)
                {
                    VisitModule(depModule, visited, stack, currentPath);
                }
            }
        }

        currentPath.Remove(module.Name);
        visited[module.Name] = true;
        stack.Add(module);
    }

    private Task TransitionStateAsync(string moduleName, ModuleState newState)
    {
        var previousState = _states.TryGetValue(moduleName, out var state) ? state : ModuleState.Registered;
        if (previousState != newState)
        {
            _states[moduleName] = newState;
            NotifyStateChanged(moduleName, previousState, newState);
        }
        return Task.CompletedTask;
    }

    private void NotifyStateChanged(string moduleName, ModuleState oldState, ModuleState newState)
    {
        try
        {
            ModuleStateChanged?.Invoke(moduleName, oldState, newState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred executing ModuleStateChanged event handlers for {ModuleName}.", moduleName);
        }
    }
}
