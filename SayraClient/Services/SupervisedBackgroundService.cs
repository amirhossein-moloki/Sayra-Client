using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

/// <summary>
/// A base background service optimized for execution within the Worker Supervisor and Health Monitor.
/// </summary>
public abstract class SupervisedBackgroundService : BackgroundService
{
    protected readonly ILogger _logger;
    protected readonly IServiceHealthMonitor _healthMonitor;
    protected readonly string _serviceName;

    protected SupervisedBackgroundService(ILogger logger, IServiceHealthMonitor healthMonitor, string serviceName)
    {
        _logger = logger;
        _healthMonitor = healthMonitor;
        _serviceName = serviceName;
    }

    /// <summary>
    /// Executes the background service logic inside the supervisor, reporting health transitions and letting failures bubble up.
    /// </summary>
    public virtual async Task RunSupervisedAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Supervised worker '{ServiceName}' starting...", _serviceName);
        _healthMonitor.ReportState(_serviceName, ServiceHealthState.Starting, "Supervised worker starting.");

        try
        {
            await ExecuteAsync(cancellationToken);
            _healthMonitor.ReportState(_serviceName, ServiceHealthState.Stopped, "Supervised worker stopped gracefully.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _healthMonitor.ReportState(_serviceName, ServiceHealthState.Stopped, "Supervised worker stopped via cancellation.");
            throw;
        }
        catch (Exception ex)
        {
            _healthMonitor.ReportFailure(_serviceName, ex, $"Supervised worker crashed: {ex.Message}");
            throw;
        }
    }
}
