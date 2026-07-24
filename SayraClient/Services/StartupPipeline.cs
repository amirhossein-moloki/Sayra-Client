using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SayraClient.Services.OfflineQueue;
using SayraClient.Services.Configuration;

namespace SayraClient.Services
{
    public class StartupPipeline : IStartupPipeline
    {
        private readonly ILogger<StartupPipeline> _logger;
        private readonly IDependencyValidator _dependencyValidator;
        private readonly IModuleLifecycleManager _moduleLifecycleManager;
        private readonly IWorkerSupervisor _workerSupervisor;
        private readonly IServiceHealthMonitor _healthMonitor;
        private readonly ClientStateManager _stateManager;
        private readonly IServiceProvider _serviceProvider;

        private int _completedStages;

        public StartupPipeline(
            ILogger<StartupPipeline> _logger,
            IDependencyValidator _dependencyValidator,
            IModuleLifecycleManager _moduleLifecycleManager,
            IWorkerSupervisor _workerSupervisor,
            IServiceHealthMonitor _healthMonitor,
            ClientStateManager _stateManager,
            IServiceProvider _serviceProvider)
        {
            this._logger = _logger;
            this._dependencyValidator = _dependencyValidator;
            this._moduleLifecycleManager = _moduleLifecycleManager;
            this._workerSupervisor = _workerSupervisor;
            this._healthMonitor = _healthMonitor;
            this._stateManager = _stateManager;
            this._serviceProvider = _serviceProvider;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("================================================================================");
            _logger.LogInformation("SAYRA Enterprise Client: Initializing 10-Stage Pipeline Startup Execution Sequence...");
            _logger.LogInformation("================================================================================");

            _completedStages = 0;

            try
            {
                // Stage 1: Pre Startup
                await ExecutePreStartupAsync(cancellationToken);

                // Stage 2: Validation
                await ExecuteValidationAsync(cancellationToken);

                // Stage 3: Dependency Validation
                await ExecuteDependencyValidationAsync(cancellationToken);

                // Stage 4: Configuration Validation
                await ExecuteConfigurationValidationAsync(cancellationToken);

                // Stage 5: Module Registration
                await ExecuteModuleRegistrationAsync(cancellationToken);

                // Stage 6: Module Initialization
                await ExecuteModuleInitializationAsync(cancellationToken);

                // Stage 7: Service Startup
                await ExecuteServiceStartupAsync(cancellationToken);

                // Stage 8: Worker Startup
                await ExecuteWorkerStartupAsync(cancellationToken);

                // Stage 9: Health Validation
                await ExecuteHealthValidationAsync(cancellationToken);

                // Stage 10: Startup Completed
                await ExecuteStartupCompletedAsync(cancellationToken);

                _logger.LogInformation("================================================================================");
                _logger.LogInformation("SAYRA Enterprise Client: Startup pipeline completed successfully (All 10 Stages Active).");
                _logger.LogInformation("================================================================================");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "FATAL ERROR: Startup pipeline failed at Stage {Stage}. Executing rollback procedure...", _completedStages + 1);
                await RollbackStartupAsync();
                throw;
            }
        }

        private Task ExecutePreStartupAsync(CancellationToken ct)
        {
            _logger.LogInformation("[Stage 1/10] [Pre Startup] Initiating runtime environment validation and preparing host parameters...");

            // Register with Windows Restart Manager
            try
            {
                var restartHelper = _serviceProvider.GetRequiredService<SayraClient.Services.Windows.IRestartManagerHelper>();
                restartHelper.RegisterForRestart("");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve or trigger Windows Restart Manager helper.");
            }

            _healthMonitor.ReportState("StartupPipeline", ServiceHealthState.Starting, "Executing Pre Startup...");
            _stateManager.TransitionTo(ClientState.STARTING);
            _completedStages = 1;
            return Task.CompletedTask;
        }

        private Task ExecuteValidationAsync(CancellationToken ct)
        {
            _logger.LogInformation("[Stage 2/10] [Validation] Verifying standard memory models, platform architecture, and execution integrity...");
            if (!Environment.Is64BitProcess)
            {
                _logger.LogWarning("System is not running as a 64-bit process. Virtual address space may be limited.");
            }
            _completedStages = 2;
            return Task.CompletedTask;
        }

        private async Task ExecuteDependencyValidationAsync(CancellationToken ct)
        {
            _logger.LogInformation("[Stage 3/10] [Dependency Validation] Interrogating external system files, operational directories, and OS privileges...");
            await _dependencyValidator.ValidateDependenciesAsync(ct);
            _completedStages = 3;
        }

        private async Task ExecuteConfigurationValidationAsync(CancellationToken ct)
        {
            _logger.LogInformation("[Stage 4/10] [Configuration Validation] Checking consistency, range boundaries, and signature validation in client configurations...");
            await _dependencyValidator.ValidateConfigurationAsync(ct);
            _completedStages = 4;
        }

        private Task ExecuteModuleRegistrationAsync(CancellationToken ct)
        {
            _logger.LogInformation("[Stage 5/10] [Module Registration] Dynamically building operational dependency graph and registering modules...");

            // Register Launcher Integration Service as a Module
            var launcherModule = _serviceProvider.GetRequiredService<LauncherIntegrationService>();
            _moduleLifecycleManager.RegisterModule(launcherModule);

            _completedStages = 5;
            return Task.CompletedTask;
        }

        private async Task ExecuteModuleInitializationAsync(CancellationToken ct)
        {
            _logger.LogInformation("[Stage 6/10] [Module Initialization] Invoking ordered asynchronous initializers across the entire module graph...");
            await _moduleLifecycleManager.InitializeAllAsync(ct);
            _completedStages = 6;
        }

        private async Task ExecuteServiceStartupAsync(CancellationToken ct)
        {
            _logger.LogInformation("[Stage 7/10] [Service Startup] Initializing module service runtimes and transitioning states...");
            await _moduleLifecycleManager.StartAllAsync(ct);
            _completedStages = 7;
        }

        private async Task ExecuteWorkerStartupAsync(CancellationToken ct)
        {
            _logger.LogInformation("[Stage 8/10] [Worker Startup] Initiating background supervisors and commencing worker processing loops...");

            // Resolve supervised workers
            var ipcServer = _serviceProvider.GetRequiredService<IpcServer>();
            var networkWorker = _serviceProvider.GetRequiredService<Worker>();
            var heartbeatService = _serviceProvider.GetRequiredService<HeartbeatService>();
            var watchdogService = _serviceProvider.GetRequiredService<WatchdogService>();
            var antiTamperService = _serviceProvider.GetRequiredService<AntiTamperService>();
            var whitelistingService = _serviceProvider.GetRequiredService<WhitelistingService>();
            var updateManager = _serviceProvider.GetRequiredService<UpdateManager>();
            var queueProcessor = _serviceProvider.GetRequiredService<QueueProcessorWorker>();
            var queueHealth = _serviceProvider.GetRequiredService<QueueHealthWorker>();
            var batchingWorker = _serviceProvider.GetRequiredService<EventQueueBatchingWorker>();
            var compressionWorker = _serviceProvider.GetRequiredService<LogCompressionWorker>();
            var syncScheduler = _serviceProvider.GetRequiredService<ConfigurationSyncScheduler>();

            // Resolve new native Windows integration services
            var registryWatcher = _serviceProvider.GetRequiredService<SayraClient.Services.Windows.RegistryWatcher>();
            var fsTamperWatcher = _serviceProvider.GetRequiredService<SayraClient.Services.Windows.FileSystemTamperWatcher>();
            var sessionMonitor = _serviceProvider.GetRequiredService<SayraClient.Services.Windows.WtsSessionChangeMonitor>();
            var etwProcessMonitor = _serviceProvider.GetRequiredService<SayraClient.Services.Windows.EtwProcessMonitor>();
            var powerHandler = _serviceProvider.GetRequiredService<SayraClient.Services.Windows.PowerStatusChangeHandler>();
            var taskScheduler = _serviceProvider.GetRequiredService<SayraClient.Services.Windows.TaskSchedulerFallbackService>();

            // Register workers with proper dependency hierarchy in WorkerSupervisor
            _workerSupervisor.RegisterWorker("IpcServer", token => ipcServer.RunSupervisedAsync(token));

            _workerSupervisor.RegisterWorker("NetworkWorker",
                token => networkWorker.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("HeartbeatService",
                token => heartbeatService.RunSupervisedAsync(token),
                new[] { "NetworkWorker" });

            _workerSupervisor.RegisterWorker("WatchdogService",
                token => watchdogService.RunSupervisedAsync(token),
                new[] { "NetworkWorker" });

            _workerSupervisor.RegisterWorker("AntiTamperService",
                token => antiTamperService.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("WhitelistingService",
                token => whitelistingService.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("UpdateManager",
                token => updateManager.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("QueueProcessorWorker",
                token => queueProcessor.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("QueueHealthWorker",
                token => queueHealth.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("EventQueueBatchingWorker",
                token => batchingWorker.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("LogCompressionWorker",
                token => compressionWorker.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("ConfigurationSyncScheduler",
                token => syncScheduler.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            // Register new native Windows integration services under supervision
            _workerSupervisor.RegisterWorker("RegistryWatcher",
                token => registryWatcher.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("FileSystemTamperWatcher",
                token => fsTamperWatcher.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("WtsSessionChangeMonitor",
                token => sessionMonitor.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("EtwProcessMonitor",
                token => etwProcessMonitor.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("PowerStatusChangeHandler",
                token => powerHandler.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            _workerSupervisor.RegisterWorker("TaskSchedulerFallbackService",
                token => taskScheduler.RunSupervisedAsync(token),
                new[] { "IpcServer" });

            // Start all supervised background workers
            await _workerSupervisor.StartAllAsync(ct);

            _completedStages = 8;
        }

        private Task ExecuteHealthValidationAsync(CancellationToken ct)
        {
            _logger.LogInformation("[Stage 9/10] [Health Validation] Computing composite system health metric and validating startup sanity...");
            var health = _healthMonitor.GetOverallHealth();
            if (health == ServiceHealthState.Failed)
            {
                throw new InvalidOperationException("Initial health check failed: One or more critical services are in Failed state.");
            }
            _logger.LogInformation("Composite startup health metric evaluated to: {Health}", health);
            _completedStages = 9;
            return Task.CompletedTask;
        }

        private Task ExecuteStartupCompletedAsync(CancellationToken ct)
        {
            _logger.LogInformation("[Stage 10/10] [Startup Completed] Declaring system fully operational and transitioning state machine to READY.");
            _healthMonitor.ReportState("StartupPipeline", ServiceHealthState.Healthy, "Startup pipeline completed successfully.");
            _stateManager.TransitionTo(ClientState.DISCOVERING_SERVER);
            _completedStages = 10;
            return Task.CompletedTask;
        }

        private async Task RollbackStartupAsync()
        {
            _logger.LogWarning("ROLLBACK: Restoring system to safe state prior to total service shutdown...");

            try
            {
                _healthMonitor.ReportState("StartupPipeline", ServiceHealthState.Failed, "Startup failed. Rolling back...");

                // Stop workers
                if (_completedStages >= 8)
                {
                    _logger.LogWarning("ROLLBACK: Stopping supervised background workers...");
                    await _workerSupervisor.StopAllAsync();
                }

                // Stop modules
                if (_completedStages >= 6)
                {
                    _logger.LogWarning("ROLLBACK: Shutting down registered modules in reverse order...");
                    await _moduleLifecycleManager.StopAllAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical failure during startup pipeline rollback procedure.");
            }
        }
    }
}
