using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class ShutdownCoordinator : IShutdownCoordinator
{
    private readonly ILogger<ShutdownCoordinator> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IWorkerSupervisor _workerSupervisor;
    private readonly IModuleLifecycleManager _moduleLifecycleManager;
    private readonly IServiceHealthMonitor _healthMonitor;
    private readonly SessionManager _sessionManager;

    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);
    private int _shutdownInProgress;

    public ShutdownCoordinator(
        ILogger<ShutdownCoordinator> logger,
        IHostApplicationLifetime hostApplicationLifetime,
        IWorkerSupervisor workerSupervisor,
        IModuleLifecycleManager moduleLifecycleManager,
        IServiceHealthMonitor healthMonitor,
        SessionManager sessionManager)
    {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
        _workerSupervisor = workerSupervisor;
        _moduleLifecycleManager = moduleLifecycleManager;
        _healthMonitor = healthMonitor;
        _sessionManager = sessionManager;
    }

    public async Task InitiateShutdownAsync(string reason, int exitCode = 0)
    {
        // Thread-safe guard to ensure we only run the shutdown sequence once
        if (Interlocked.Exchange(ref _shutdownInProgress, 1) != 0)
        {
            _logger.LogWarning("Graceful shutdown already in progress. Ignoring duplicate request. Reason: {Reason}", reason);
            return;
        }

        _logger.LogWarning("================================================================================");
        _logger.LogWarning("SAYRA Enterprise Client: Graceful Shutdown Initiated. Reason: '{Reason}'", reason);
        _logger.LogWarning("================================================================================");

        _healthMonitor.ReportState("ShutdownCoordinator", ServiceHealthState.Stopped, $"Graceful shutdown initiated: {reason}");

        using var cts = new CancellationTokenSource(ShutdownTimeout);
        var ct = cts.Token;

        try
        {
            // Stage 1: Save Runtime State & Stop Session
            _logger.LogInformation("[Shutdown] Saving operational runtime state and persisting local configurations...");
            var currentSession = _sessionManager.GetCurrentSession();
            if (currentSession != null && currentSession.Status == "ACTIVE")
            {
                _logger.LogInformation("[Shutdown] Active session detected. Saving state...");
                _sessionManager.StopSession("GRACEFUL_SHUTDOWN");
            }

            // Stage 2: Stop Supervised Workers
            _logger.LogInformation("[Shutdown] Discontinuing and stopping supervised background workers in reverse dependency order...");
            await _workerSupervisor.StopAllAsync();

            // Stage 3: Stop Modules
            _logger.LogInformation("[Shutdown] Terminating modular services gracefully...");
            await _moduleLifecycleManager.StopAllAsync(ct);

            // Stage 4: Telemetry & Connection Flush
            _logger.LogInformation("[Shutdown] Flushing system diagnostics telemetry and local IPC buffers...");

            // Stage 5: Flush Logs & Stop Generic Host
            _logger.LogWarning("================================================================================");
            _logger.LogWarning("SAYRA Enterprise Client: Graceful Shutdown sequence completed. Stopping application host.");
            _logger.LogWarning("================================================================================");

            Serilog.Log.CloseAndFlush();

            _hostApplicationLifetime.StopApplication();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR: Graceful shutdown sequence encountered an error. Falling back to Emergency Shutdown.");
            EmergencyShutdown($"Graceful shutdown exception: {ex.Message}", exitCode);
        }
    }

    public void EmergencyShutdown(string reason, int exitCode = -1)
    {
        _logger.LogCritical("================================================================================");
        _logger.LogCritical("CRITICAL FALLBACK: EMERGENCY SHUTDOWN DIRECTED! Reason: {Reason}", reason);
        _logger.LogCritical("================================================================================");

        try
        {
            Serilog.Log.CloseAndFlush();
        }
        catch
        {
            // Best effort log flush during panic
        }

        Environment.Exit(exitCode);
    }
}
