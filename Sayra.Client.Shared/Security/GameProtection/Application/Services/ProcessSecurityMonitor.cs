using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;
using Sayra.Client.Shared.Security.GameProtection.Domain.Events;
using Sayra.Client.Shared.Security.GameProtection.Domain.Models;
using Sayra.Client.Shared.Security.GameProtection.Domain.Rules;

namespace Sayra.Client.Shared.Security.GameProtection.Application.Services;

public class ProcessSecurityMonitor : IProcessSecurityMonitor
{
    private readonly ILogger<ProcessSecurityMonitor> _logger;
    private readonly IProcessPolicyEvaluator _policyEvaluator;
    private readonly IThreatReporter _threatReporter;
    private readonly IIntegrityValidator _integrityValidator;
    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;

    public ProcessSecurityMonitor(
        ILogger<ProcessSecurityMonitor> logger,
        IProcessPolicyEvaluator policyEvaluator,
        IThreatReporter threatReporter,
        IIntegrityValidator integrityValidator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _policyEvaluator = policyEvaluator ?? throw new ArgumentNullException(nameof(policyEvaluator));
        _threatReporter = threatReporter ?? throw new ArgumentNullException(nameof(threatReporter));
        _integrityValidator = integrityValidator ?? throw new ArgumentNullException(nameof(integrityValidator));
    }

    public Task StartMonitoringAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Process Security Monitor...");
        _cts = new CancellationTokenSource();
        _monitoringTask = Task.Run(() => MonitoringLoopAsync(_cts.Token), cancellationToken);
        return Task.CompletedTask;
    }

    public async Task StopMonitoringAsync()
    {
        _logger.LogInformation("Stopping Process Security Monitor...");
        if (_cts != null)
        {
            _cts.Cancel();
        }

        if (_monitoringTask != null)
        {
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when canceling Task.Delay
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Process Security Monitor.");
            }
        }
    }

    private async Task MonitoringLoopAsync(CancellationToken token)
    {
        _logger.LogInformation("Process Security Monitor loop started.");

        while (!token.IsCancellationRequested)
        {
            try
            {
                EvaluateCurrentProcesses();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating processes during monitoring interval.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Process Security Monitor loop stopped.");
    }

    public void EvaluateCurrentProcesses()
    {
        // Safe, non-blocking evaluation of processes on the workstation
        // We retrieve the running process snapshot using .NET standard Process.GetProcesses()
        // without killing them directly. Direct termination is delegated to the Process Supervisor (Track 4.3).
        var runningProcesses = Process.GetProcesses();

        foreach (var p in runningProcesses)
        {
            try
            {
                // Standard validation parameters.
                // We handle Win32Exceptions (Access Denied) when trying to access paths of system processes.
                string processName = p.ProcessName;
                int pid = p.Id;
                string exePath = string.Empty;

                try
                {
                    exePath = p.MainModule?.FileName ?? string.Empty;
                }
                catch
                {
                    // Swallowing access-denied exception for privileged processes
                }

                var procInfo = new ProcessInfo
                {
                    ProcessId = pid,
                    ProcessName = processName,
                    ExecutablePath = exePath
                };

                var decision = _policyEvaluator.Evaluate(procInfo);

                if (decision.Action == ProcessAction.Terminate || decision.Action == ProcessAction.Block)
                {
                    _logger.LogWarning("Security violation found! Action needed: {Action} for {Process} ({Pid}). Reason: {Reason}",
                        decision.Action, processName, pid, decision.Reason);

                    // Map threat events
                    SecurityThreatEventBase threatEvent;
                    if (decision.RuleTriggered != null && !string.IsNullOrEmpty(decision.RuleTriggered.PathPattern))
                    {
                        threatEvent = new BlockedApplicationDetectedEvent
                        {
                            ProcessId = pid,
                            ProcessName = processName,
                            Reason = decision.Reason,
                            Severity = decision.Severity,
                            RulePatternMatched = decision.RuleTriggered.PathPattern
                        };
                    }
                    else if (decision.Reason.Contains("integrity", StringComparison.OrdinalIgnoreCase))
                    {
                        threatEvent = new IntegrityCheckFailedEvent
                        {
                            ProcessId = pid,
                            ProcessName = processName,
                            Reason = decision.Reason,
                            Severity = decision.Severity,
                            FilePath = exePath
                        };
                    }
                    else
                    {
                        threatEvent = new UnauthorizedProcessDetectedEvent
                        {
                            ProcessId = pid,
                            ProcessName = processName,
                            Reason = decision.Reason,
                            Severity = decision.Severity,
                            ExecutablePath = exePath
                        };
                    }

                    _threatReporter.ReportThreat(threatEvent);
                }
            }
            catch (Exception ex)
            {
                // Silently swallow errors on a per-process evaluation basis to prevent loop termination
                _logger.LogTrace(ex, "Skipped processing process ID {Pid}", p.Id);
            }
        }
    }
}
