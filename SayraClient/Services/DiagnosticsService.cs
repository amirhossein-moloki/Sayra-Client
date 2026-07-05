using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SayraClient.Models;
using System.Net.NetworkInformation;

namespace SayraClient.Services;

public class DiagnosticsService
{
    private readonly ILogger<DiagnosticsService> _logger;
    private readonly SessionManager _sessionManager;
    private readonly ProcessMonitor _processMonitor;
    private readonly Process _currentProcess;

    public DiagnosticsService(ILogger<DiagnosticsService> logger, SessionManager sessionManager, ProcessMonitor processMonitor)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _processMonitor = processMonitor;
        _currentProcess = Process.GetCurrentProcess();
    }

    public TelemetryModel GetDiagnostics()
    {
        return new TelemetryModel
        {
            Cpu = GetCpuUsage(),
            Ram = (double)_currentProcess.WorkingSet64 / (1024 * 1024),
            Uptime = (int)(DateTime.Now - _currentProcess.StartTime).TotalSeconds,
            Timestamp = DateTime.UtcNow
        };
    }

    private double GetCpuUsage()
    {
        var startTime = _currentProcess.StartTime;
        var totalCpuTime = _currentProcess.TotalProcessorTime;
        var elapsed = DateTime.Now - startTime;

        if (elapsed.TotalMilliseconds == 0) return 0;

        return Math.Round(totalCpuTime.TotalMilliseconds / (Environment.ProcessorCount * elapsed.TotalMilliseconds) * 100, 2);
    }
}
