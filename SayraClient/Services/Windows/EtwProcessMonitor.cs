using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Services.Windows;

public class EtwProcessMonitor : SupervisedBackgroundService
{
    private readonly IAuditLogger _auditLogger;
    private readonly KioskManager _kioskManager;
    private TraceEventSession? _etwSession;
    private ManagementEventWatcher? _wmiWatcher;
    private readonly HashSet<string> _blacklistedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "cheatengine", "processhacker", "wireshark", "fiddler", "ollydbg", "x64dbg", "regedit"
    };

    public EtwProcessMonitor(
        ILogger<EtwProcessMonitor> logger,
        IServiceHealthMonitor healthMonitor,
        IAuditLogger auditLogger,
        KioskManager kioskManager)
        : base(logger, healthMonitor, "EtwProcessMonitor")
    {
        _auditLogger = auditLogger;
        _kioskManager = kioskManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EtwProcessMonitor (Process creation / socket hijacking monitor) starting...");

        if (OperatingSystem.IsWindows())
        {
            // Attempt real Windows ETW Kernel Session first, as requested for maximum secure isolation
            StartEtwKernelSession(stoppingToken);
        }
        else
        {
            _logger.LogWarning("EtwProcessMonitor native Windows event watching is not supported on this platform. Running cross-platform simulated mode.");
            StartPollingProcessMonitor(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            _healthMonitor.ReportHeartbeat(_serviceName);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private void StartEtwKernelSession(CancellationToken ct)
    {
        _ = Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Starting real Event Tracing for Windows (ETW) Kernel Session...");

                var sessionName = "SAYRA_Kernel_Process_Trace_Session";

                // Ensure any leftover active sessions of the same name are cleared first
                try
                {
                    var existingSession = TraceEventSession.GetActiveSessionNames();
                    if (existingSession != null)
                    {
                        foreach (var name in existingSession)
                        {
                            if (name == sessionName)
                            {
                                var oldSession = new TraceEventSession(sessionName);
                                oldSession.Dispose();
                            }
                        }
                    }
                }
                catch { /* Ignore cleanup issues */ }

                _etwSession = new TraceEventSession(sessionName);
                _etwSession.EnableKernelProvider(KernelTraceEventParser.Keywords.Process);

                _etwSession.Source.Kernel.ProcessStart += (ProcessTraceData data) =>
                {
                    EvaluateProcess(data.ProcessName, data.ProcessID);
                };

                _logger.LogInformation("Real Windows ETW Session started. Process start callback registered.");

                // Start processing trace events on a background thread
                Task.Run(() => _etwSession.Source.Process(), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start real Windows ETW session (requires elevated SYSTEM/Admin privileges). Falling back to WMI Event Watcher.");
                StartWmiWatcher();
            }
        }, ct);
    }

    private void StartWmiWatcher()
    {
        try
        {
            string query = "SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_Process'";
            _wmiWatcher = new ManagementEventWatcher(new EventQuery(query));
            _wmiWatcher.EventArrived += OnProcessCreated;
            _wmiWatcher.Start();

            _logger.LogInformation("Successfully started Win32_Process creation WMI event watcher fallback.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize WMI process watcher. Falling back to polling process monitor.");
            // Create a fake/mock cancellation token source for background polling fallback if needed
            var cts = new CancellationTokenSource();
            StartPollingProcessMonitor(cts.Token);
        }
    }

    private void StartPollingProcessMonitor(CancellationToken ct)
    {
        var activeProcesses = new HashSet<int>();
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var current = Process.GetProcesses();
                    foreach (var proc in current)
                    {
                        if (activeProcesses.Add(proc.Id))
                        {
                            EvaluateProcess(proc.ProcessName, proc.Id);
                        }
                    }
                    var currentIds = new HashSet<int>();
                    foreach (var p in current) currentIds.Add(p.Id);
                    activeProcesses.RemoveWhere(id => !currentIds.Contains(id));
                }
                catch
                {
                    // Ignore transient process access errors
                }

                await Task.Delay(2000, ct);
            }
        }, ct);
    }

    private void OnProcessCreated(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var targetInstance = e.NewEvent["TargetInstance"] as ManagementBaseObject;
            if (targetInstance != null)
            {
                string name = targetInstance["Name"]?.ToString() ?? string.Empty;
                string processIdStr = targetInstance["ProcessId"]?.ToString() ?? "0";
                int pid = int.TryParse(processIdStr, out var parsedPid) ? parsedPid : 0;

                string procName = Path.GetFileNameWithoutExtension(name);
                EvaluateProcess(procName, pid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling process creation event in WMI callback.");
        }
    }

    public void EvaluateProcess(string processName, int pid)
    {
        _logger.LogDebug("Process monitored: {Name} (PID: {Pid})", processName, pid);

        if (_blacklistedProcesses.Contains(processName))
        {
            string alertMsg = $"SECURITY CRITICAL: Unauthorized process creation blocked! Process: {processName} (PID: {pid}).";
            _logger.LogWarning("{Message}", alertMsg);
            _auditLogger.LogSecurity(alertMsg);

            // Attempt to terminate the unauthorized process immediately in lockdown mode
            if (_kioskManager.IsLocked())
            {
                try
                {
                    using var proc = Process.GetProcessById(pid);
                    if (proc != null && !proc.HasExited)
                    {
                        proc.Kill(true);
                        _logger.LogInformation("Successfully terminated unauthorized process '{Name}' (PID: {Pid}) during kiosk session.", processName, pid);
                        _auditLogger.LogSecurity($"Tamper action executed: Automatically terminated unauthorized process '{processName}' (PID: {pid}).");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to automatically terminate unauthorized process '{Name}' (PID: {Pid}).", processName, pid);
                }
            }
        }
    }

    public override void Dispose()
    {
        if (_etwSession != null)
        {
            try
            {
                _etwSession.Stop();
                _etwSession.Dispose();
            }
            catch { /* Suppress stop errors */ }
        }

        if (_wmiWatcher != null)
        {
            try
            {
                _wmiWatcher.Stop();
                _wmiWatcher.Dispose();
            }
            catch { /* Suppress WMI watcher stop exceptions */ }
        }
        base.Dispose();
    }
}
