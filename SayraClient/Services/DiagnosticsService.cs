using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SayraClient.Models;
using System.Net.NetworkInformation;
using Sayra.Client.Launcher.Services;

namespace SayraClient.Services;

public class DiagnosticsService
{
    private readonly ILogger<DiagnosticsService> _logger;
    private readonly SessionManager _sessionManager;
    private readonly IProcessMonitorService _processMonitor;
    private readonly Process _currentProcess;

    public DiagnosticsService(ILogger<DiagnosticsService> logger, SessionManager sessionManager, IProcessMonitorService processMonitor)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _processMonitor = processMonitor;
        _currentProcess = Process.GetCurrentProcess();
    }

    public TelemetryModel GetDiagnostics()
    {
        var model = new TelemetryModel
        {
            Cpu = GetCpuUsage(),
            Ram = (double)_currentProcess.WorkingSet64 / (1024 * 1024),
            Uptime = (int)(DateTime.Now - _currentProcess.StartTime).TotalSeconds,
            Timestamp = DateTime.UtcNow,
            TotalLaunches = _processMonitor.GetTotalLaunches(),
            TotalCrashes = _processMonitor.GetTotalCrashes(),
            TotalRestarts = _processMonitor.GetTotalRestarts()
        };

        var activeGame = _processMonitor.GetRunningProcesses().FirstOrDefault(p => p.IsRunning);
        if (activeGame != null)
        {
            model.RunningGameName = activeGame.Name;
            model.RunningGamePid = activeGame.Pid;
            model.RunningGameCpu = activeGame.CpuUsagePercentage;
            model.RunningGameRam = activeGame.RamUsageMb;
            model.RunningGameDurationSeconds = activeGame.RunningDuration.TotalSeconds;
        }

        return model;
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
