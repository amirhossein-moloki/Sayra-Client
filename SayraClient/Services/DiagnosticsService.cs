using System.Diagnostics;
using System.Runtime.InteropServices;
using SayraClient.Services;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

using System.Net.NetworkInformation;

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

    public object GetDiagnostics()
    {
        return new
        {
            ClientVersion = GetClientVersion(),
            OSVersion = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            MemoryUsageMB = _currentProcess.WorkingSet64 / (1024 * 1024),
            CpuUsagePercent = GetCpuUsage(),
            UptimeMinutes = (DateTime.Now - _currentProcess.StartTime).TotalMinutes,
            SessionStatus = _sessionManager.GetCurrentSession()?.Status.ToString() ?? "IDLE",
            NetworkStatus = GetNetworkStatus(),
            ProcessCount = _processMonitor.GetRunningProcesses().Count(),
            Timestamp = DateTime.UtcNow
        };
    }

    private object GetNetworkStatus()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
            .Select(ni => new
            {
                Name = ni.Name,
                Type = ni.NetworkInterfaceType.ToString(),
                Speed = ni.Speed
            }).FirstOrDefault() ?? new { Name = "Unknown", Type = "None", Speed = 0L };
    }

    private string GetClientVersion()
    {
        return typeof(DiagnosticsService).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    private double GetCpuUsage()
    {
        // Simple CPU usage estimation: TotalProcessorTime / Time since start
        // Note: For a more accurate real-time CPU usage, we'd need multiple samples
        var startTime = _currentProcess.StartTime;
        var totalCpuTime = _currentProcess.TotalProcessorTime;
        var elapsed = DateTime.Now - startTime;

        if (elapsed.TotalMilliseconds == 0) return 0;

        return Math.Round(totalCpuTime.TotalMilliseconds / (Environment.ProcessorCount * elapsed.TotalMilliseconds) * 100, 2);
    }
}
