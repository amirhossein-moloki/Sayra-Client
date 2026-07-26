using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Events;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.States;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Services
{
    public class ProcessSupervisor : IProcessSupervisor, IDisposable
    {
        private readonly ILogger<ProcessSupervisor> _logger;
        private readonly IRuntimeEventPublisher _eventPublisher;
        private readonly IJobObjectManager _jobManager;
        private readonly IProcessTreeMonitor _treeMonitor;
        private readonly IProcessResourceMonitor _resourceMonitor;
        private readonly IOptions<ProcessSupervisorOptions> _options;

        private readonly ConcurrentDictionary<Guid, ProcessStatus> _statuses = new();
        private readonly ConcurrentDictionary<Guid, ProcessInfo> _processes = new();
        private readonly CancellationTokenSource _cts = new();
        private bool _disposed;

        public ProcessSupervisor(
            ILogger<ProcessSupervisor> logger,
            IRuntimeEventPublisher eventPublisher,
            IJobObjectManager jobManager,
            IProcessTreeMonitor treeMonitor,
            IProcessResourceMonitor resourceMonitor)
            : this(logger, eventPublisher, jobManager, treeMonitor, resourceMonitor, null)
        {
        }

        public ProcessSupervisor(
            ILogger<ProcessSupervisor> logger,
            IRuntimeEventPublisher eventPublisher,
            IJobObjectManager jobManager,
            IProcessTreeMonitor treeMonitor,
            IProcessResourceMonitor resourceMonitor,
            IOptions<ProcessSupervisorOptions>? options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
            _jobManager = jobManager ?? throw new ArgumentNullException(nameof(jobManager));
            _treeMonitor = treeMonitor ?? throw new ArgumentNullException(nameof(treeMonitor));
            _resourceMonitor = resourceMonitor ?? throw new ArgumentNullException(nameof(resourceMonitor));
            _options = options ?? Microsoft.Extensions.Options.Options.Create(new ProcessSupervisorOptions());

            // Start a low-overhead background monitor thread to track process lifetimes
            Task.Run(MonitorLifetimesAsync, _cts.Token);
        }

        public async Task RegisterAsync(ProcessInfo process)
        {
            if (process == null) throw new ArgumentNullException(nameof(process));
            if (_disposed) throw new ObjectDisposedException(nameof(ProcessSupervisor));

            _logger.LogInformation("Registering process {ProcessId} ({ProcessName}) for RuntimeId '{RuntimeId}'",
                process.ProcessId, process.ProcessName, process.RuntimeId);

            // Clear any old/existing records since only one game runs at a time on a kiosk workstation
            _statuses.Clear();
            _processes.Clear();

            var status = new ProcessStatus
                {
                    RuntimeId = process.RuntimeId,
                    ProcessId = process.ProcessId,
                    State = ProcessState.Created,
                    StartTime = DateTime.UtcNow,
                    Details = "Process registered with Process Supervisor."
                };

            _statuses[process.RuntimeId] = status;
            _processes[process.RuntimeId] = process;

            // Trigger registration event
            _eventPublisher.Publish(new ProcessRegisteredEvent(process.RuntimeId, process.ProcessId, "Registered in Process Supervisor."));

            try
            {
                // Transition: Created -> Starting
                TransitionState(status, ProcessState.Starting, "Initiating Job Object assignment.");
                _eventPublisher.Publish(new ProcessStartedEvent(process.RuntimeId, process.ProcessId, "Process starting."));

                // 1. Create Job Object
                _jobManager.CreateJob(process.RuntimeId);

                // Configure Limits
                var opts = _options.Value;
                _logger.LogInformation("Configuring Job Object limits for RuntimeId: '{RuntimeId}'. MaxMemoryBytes: {MaxMemory}, CpuAffinityMask: {Affinity}",
                    process.RuntimeId, opts.MaxMemoryBytes, opts.CpuAffinityMask);
                _jobManager.ConfigureLimits(process.RuntimeId, opts.MaxMemoryBytes, opts.CpuAffinityMask);

                // Apply priority rules
                ApplyPriority(process.ProcessId, opts.PriorityClass);

                // 2. Assign process to Job Object
                _jobManager.AssignProcess(process.RuntimeId, process.ProcessId);

                // Transition: Starting -> Running
                TransitionState(status, ProcessState.Running, "Process successfully locked inside Windows Job Object.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to completely register process {ProcessId} in Job Object.", process.ProcessId);
                TransitionState(status, ProcessState.Crashed, $"Failed to bind to Job Object: {ex.Message}");
                _eventPublisher.Publish(new ProcessCrashedEvent(process.RuntimeId, process.ProcessId, -1, $"Failed to bind: {ex.Message}"));
                throw;
            }
        }

        private void ApplyPriority(int processId, string priorityStr)
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return;
            }

            try
            {
                using (var proc = Process.GetProcessById(processId))
                {
                    if (Enum.TryParse<ProcessPriorityClass>(priorityStr, true, out var priorityClass))
                    {
                        proc.PriorityClass = priorityClass;
                        _logger.LogInformation("Successfully configured process priority to: '{Priority}' for PID: {PID}", priorityClass, processId);
                    }
                    else
                    {
                        _logger.LogWarning("Invalid priority class name configured: '{Priority}'", priorityStr);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply priority '{Priority}' to process {PID}.", priorityStr, processId);
            }
        }

        public async Task StopAsync(Guid runtimeId)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ProcessSupervisor));

            if (!_statuses.TryGetValue(runtimeId, out var status))
            {
                _logger.LogWarning("StopAsync: No process found registered with RuntimeId '{RuntimeId}'", runtimeId);
                return;
            }

            _logger.LogInformation("Stopping process {ProcessId} for RuntimeId '{RuntimeId}'", status.ProcessId, runtimeId);

            try
            {
                // Transition: State -> Stopping
                TransitionState(status, ProcessState.Stopping, "Termination requested.");

                // Terminate Job Object (which automatically kills all children in the tree)
                _jobManager.TerminateJob(runtimeId);

                // Transition: Stopping -> Stopped
                TransitionState(status, ProcessState.Stopped, "Process tree terminated via Job Object.");
                _eventPublisher.Publish(new ProcessExitedEvent(runtimeId, status.ProcessId, 0, "Gracefully stopped via Job Object termination."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while stopping process tree for RuntimeId '{RuntimeId}'", runtimeId);
                TransitionState(status, ProcessState.Unknown, $"Error during stop: {ex.Message}");
            }
        }

        public Task<ProcessStatus> GetStatusAsync(Guid runtimeId)
        {
            if (_statuses.TryGetValue(runtimeId, out var status))
            {
                return Task.FromResult(status);
            }

            return Task.FromResult(new ProcessStatus
            {
                RuntimeId = runtimeId,
                ProcessId = 0,
                State = ProcessState.Unknown,
                StartTime = DateTime.MinValue,
                Details = "No process registered with this RuntimeId."
            });
        }

        private void TransitionState(ProcessStatus status, ProcessState targetState, string details)
        {
            var oldState = status.State;
            ProcessStateMachine.ValidateTransition(oldState, targetState);
            status.State = targetState;
            status.Details = details;
            _logger.LogInformation("Process state transition for RuntimeId '{RuntimeId}': '{OldState}' -> '{TargetState}'. Info: {Details}",
                status.RuntimeId, oldState, targetState, details);
        }

        private async Task MonitorLifetimesAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, _cts.Token);

                    foreach (var pair in _statuses)
                    {
                        var runtimeId = pair.Key;
                        var status = pair.Value;

                        if (status.State != ProcessState.Running) continue;

                        // Check if root process is still alive
                        bool isAlive = false;
                        int exitCode = 0;
                        try
                        {
                            using (var proc = Process.GetProcessById(status.ProcessId))
                            {
                                isAlive = !proc.HasExited;
                                if (!isAlive)
                                {
                                    exitCode = proc.ExitCode;
                                }
                            }
                        }
                        catch
                        {
                            // Process is gone
                            isAlive = false;
                            exitCode = -1;
                        }

                        if (!isAlive)
                        {
                            _logger.LogWarning("Root process {ProcessId} for RuntimeId '{RuntimeId}' has exited.", status.ProcessId, runtimeId);

                            // Check if it exited normally or crashed (non-zero exit code is usually a crash)
                            if (exitCode == 0)
                            {
                                TransitionState(status, ProcessState.Stopped, "Process exited normally.");
                                _eventPublisher.Publish(new ProcessExitedEvent(runtimeId, status.ProcessId, exitCode, "Root process exited normally."));
                            }
                            else
                            {
                                TransitionState(status, ProcessState.Crashed, $"Process exited with non-zero code: {exitCode}.");
                                _eventPublisher.Publish(new ProcessCrashedEvent(runtimeId, status.ProcessId, exitCode, "Root process crashed."));
                            }

                            // Clean up the job object for this run
                            try
                            {
                                _jobManager.TerminateJob(runtimeId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to clean up Job Object for RuntimeId '{RuntimeId}' after process exit.", runtimeId);
                            }
                        }
                        else
                        {
                            // Tree monitoring: check for unexpected children
                            try
                            {
                                var descendants = await _treeMonitor.GetDescendantsAsync(status.ProcessId);
                                foreach (var node in descendants)
                                {
                                    // Let's check if the child is running.
                                    // If it's an unauthorized or unexpected process name, we can trigger events
                                    if (node.ProcessName.Contains("cheat") || node.ProcessName.Contains("hack"))
                                    {
                                        _eventPublisher.Publish(new UnauthorizedChildProcessEvent(runtimeId, node.ProcessId, $"Malicious child process name detected: {node.ProcessName}"));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Failed to monitor process tree for RuntimeId '{RuntimeId}': {Msg}", runtimeId, ex.Message);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ProcessSupervisor background monitoring loop.");
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cts.Cancel();
            _cts.Dispose();

            _disposed = true;
        }
    }
}
