using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Launcher.Services;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;
using SayraClient.Services.Recovery;

namespace SayraClient.Services
{
    public class WatchdogService : SupervisedBackgroundService
    {
        private readonly RecoveryManager _recoveryManager;
        private readonly TcpClientManager _networkManager;
        private readonly IGameLauncherService _gameLauncher;
        private readonly ISelfHealingService _selfHealingService;
        private readonly IHealthMonitor _subsystemHealthMonitor;
        private readonly ResourceMonitor _resourceMonitor;
        private readonly SecurityHardeningService _securityHardeningService;
        private readonly IResilienceConfigurationProvider _resilienceConfigProvider;

        public WatchdogService(
            ILogger<WatchdogService> logger,
            RecoveryManager recoveryManager,
            TcpClientManager networkManager,
            IGameLauncherService gameLauncher,
            IServiceHealthMonitor healthMonitor,
            ISelfHealingService selfHealingService,
            IHealthMonitor subsystemHealthMonitor,
            ResourceMonitor resourceMonitor,
            SecurityHardeningService securityHardeningService,
            IResilienceConfigurationProvider? resilienceConfigProvider = null)
            : base(logger, healthMonitor, "WatchdogService")
        {
            _recoveryManager = recoveryManager;
            _networkManager = networkManager;
            _gameLauncher = gameLauncher;
            _selfHealingService = selfHealingService;
            _subsystemHealthMonitor = subsystemHealthMonitor;
            _resourceMonitor = resourceMonitor;
            _securityHardeningService = securityHardeningService;
            _resilienceConfigProvider = resilienceConfigProvider ?? new FallbackResilienceConfigurationProvider();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Enhanced Watchdog Service starting...");

            // Initial recovery on startup
            _recoveryManager.RecoverState();

            while (!stoppingToken.IsCancellationRequested)
            {
                var config = _resilienceConfigProvider.CurrentConfiguration?.Watchdog ?? new WatchdogOptions();
                try
                {
                    _healthMonitor.ReportHeartbeat("WatchdogService");
                    _subsystemHealthMonitor.ReportHeartbeat("WatchdogService");

                    _logger.LogDebug("Watchdog performing active system checks...");

                    // 1. Ensure Guardian is running
                    EnsureGuardianRunning();

                    // 2. Perform Deadlock & Frozen Worker Detection
                    if (config.EnableDeadlockDetection)
                    {
                        DetectDeadlocksAndFrozenWorkers();
                    }

                    // 3. Perform Resource Audit & Backpressure checks
                    if (config.EnableResourcePressureMitigation)
                    {
                        await _resourceMonitor.RunResourceAuditAsync(stoppingToken);
                    }

                    // 4. Perform Security Hardening & Tamper checks
                    if (config.EnableSecurityViolationAudit)
                    {
                        await _securityHardeningService.VerifySystemIntegrityAsync(stoppingToken);
                    }

                    // 5. Run Self-Healing checks
                    await _selfHealingService.MonitorAndHealAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during Watchdog execution cycle.");
                }

                await Task.Delay(config.PollingInterval, stoppingToken);
            }

            _logger.LogInformation("Watchdog Service stopping.");
        }

        private void EnsureGuardianRunning()
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("Sayra.Client.Guardian");
                if (processes.Length == 0)
                {
                    _logger.LogWarning("Sayra Guardian process not found! Restarting...");

                    string guardianPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Sayra.Client.Guardian.exe");
                    if (System.IO.File.Exists(guardianPath))
                    {
                        _ = _gameLauncher.LaunchApplicationAsync(guardianPath, "", "", false, CancellationToken.None);
                    }
                    else
                    {
                        _logger.LogError("Sayra Guardian executable not found at {Path}", guardianPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring Guardian is running.");
            }
        }

        private void DetectDeadlocksAndFrozenWorkers()
        {
            _logger.LogInformation("Watchdog: Initiating deadlock and frozen worker checks...");

            var config = _resilienceConfigProvider.CurrentConfiguration?.Watchdog ?? new WatchdogOptions();
            var detailedWorkerHealth = _healthMonitor.GetDetailedHealth();
            var now = DateTime.UtcNow;

            foreach (var kvp in detailedWorkerHealth)
            {
                var workerName = kvp.Key;
                var info = kvp.Value;

                // If a worker is healthy but hasn't updated its heartbeat for over configured timeout, consider it deadlocked/frozen
                var idleTime = now - info.LastHeartbeat;
                if (info.State == ServiceHealthState.Healthy && idleTime > config.WorkerHeartbeatTimeout)
                {
                    _logger.LogCritical("DEADLOCK/FREEZE DETECTED: Worker '{WorkerName}' has been silent for {IdleTime}s (threshold {Threshold}s). Triggering self-healing recovery.",
                        workerName, idleTime.TotalSeconds, config.WorkerHeartbeatTimeout.TotalSeconds);

                    _subsystemHealthMonitor.ReportSubsystemState("RemoteCommandEngine", SubsystemHealthState.Critical, $"Worker '{workerName}' is frozen/deadlocked.");
                }
            }
        }
    }
}
