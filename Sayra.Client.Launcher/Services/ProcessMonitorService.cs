using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Launcher.Models;

namespace Sayra.Client.Launcher.Services;

public class ProcessMonitorService : IProcessMonitorService, IDisposable
{
    private readonly ILogger<ProcessMonitorService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, TrackedProcessInfo> _trackedProcesses = new();
    private readonly Timer _monitoringTimer;

    private int _totalLaunches;
    private int _totalCrashes;
    private int _totalRestarts;

    private class TrackedProcessInfo
    {
        public string GameId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Process Process { get; set; } = null!;
        public LaunchOptions Options { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public PerformanceCounter? CpuCounter { get; set; }
        public double LastCpuValue { get; set; }
        public double LastRamValue { get; set; }
        public bool IsRunning { get; set; } = true;
        public int ExitCode { get; set; }
        public bool HasCrashed { get; set; }
        public string CrashReason { get; set; } = string.Empty;
        public int LastKnownPid { get; set; }

        // Non-blocking CPU metrics
        public TimeSpan LastTotalProcessorTime { get; set; }
        public DateTime LastCpuCheckTime { get; set; }
    }

    public ProcessMonitorService(ILogger<ProcessMonitorService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _monitoringTimer = new Timer(OnMonitorTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
    }

    public void RegisterProcess(string gameId, Process process, LaunchOptions options)
    {
        _logger.LogInformation("Registering process for GameId: '{GameId}' (PID: {PID})", gameId, process.Id);

        string procName = Path.GetFileNameWithoutExtension(options.ExecutablePath);

        TimeSpan initialProcessorTime = TimeSpan.Zero;
        try { initialProcessorTime = process.TotalProcessorTime; } catch { }

        var info = new TrackedProcessInfo
        {
            GameId = gameId,
            Name = procName,
            Process = process,
            Options = options,
            StartTime = DateTime.UtcNow,
            IsRunning = true,
            LastKnownPid = process.Id,
            LastTotalProcessorTime = initialProcessorTime,
            LastCpuCheckTime = DateTime.UtcNow
        };

        // Attempt to hook Exited event
        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += (sender, e) => HandleProcessExit(gameId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enable process exit events for game: {GameId}", gameId);
        }

        _trackedProcesses[gameId] = info;
        IncrementLaunches();

        // Force an immediate tick check to initialize counters
        _ = Task.Run(() => UpdateMetrics(info));
    }

    public void UnregisterProcess(string gameId)
    {
        if (_trackedProcesses.TryRemove(gameId, out var info))
        {
            _logger.LogInformation("Unregistered process for GameId: '{GameId}'", gameId);
            try
            {
                info.Process.Dispose();
            }
            catch { }
        }
    }

    public IEnumerable<ProcessStatistics> GetRunningProcesses()
    {
        return _trackedProcesses.Values.Select(ToStatistics).ToList();
    }

    public ProcessStatistics? GetProcessStatistics(string gameId)
    {
        if (_trackedProcesses.TryGetValue(gameId, out var info))
        {
            return ToStatistics(info);
        }
        return null;
    }

    public bool IsGameRunning(string gameId)
    {
        if (_trackedProcesses.TryGetValue(gameId, out var info))
        {
            try
            {
                return !info.Process.HasExited;
            }
            catch
            {
                return info.IsRunning;
            }
        }
        return false;
    }

    public int GetTotalLaunches() => _totalLaunches;
    public int GetTotalCrashes() => _totalCrashes;
    public int GetTotalRestarts() => _totalRestarts;

    public void IncrementLaunches() => Interlocked.Increment(ref _totalLaunches);
    public void IncrementCrashes() => Interlocked.Increment(ref _totalCrashes);
    public void IncrementRestarts() => Interlocked.Increment(ref _totalRestarts);

    private void OnMonitorTick(object? state)
    {
        foreach (var kvp in _trackedProcesses)
        {
            var info = kvp.Value;
            if (!info.IsRunning) continue;

            try
            {
                if (info.Process.HasExited)
                {
                    HandleProcessExit(info.GameId);
                }
                else
                {
                    UpdateMetrics(info);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error during monitoring tick for '{GameId}'", info.GameId);
            }
        }
    }

    private void UpdateMetrics(TrackedProcessInfo info)
    {
        try
        {
            info.Process.Refresh();
            if (!info.Process.HasExited)
            {
                // Working set (RAM)
                info.LastRamValue = (double)info.Process.WorkingSet64 / (1024 * 1024);

                // Non-blocking CPU calculation from last tick delta
                var now = DateTime.UtcNow;
                TimeSpan currentProcessorTime;
                try
                {
                    currentProcessorTime = info.Process.TotalProcessorTime;
                }
                catch
                {
                    return; // Fail-safe if process is terminating
                }

                var timeDelta = now - info.LastCpuCheckTime;
                var procDelta = currentProcessorTime - info.LastTotalProcessorTime;

                if (timeDelta.TotalMilliseconds > 0)
                {
                    double cpu = (procDelta.TotalMilliseconds / (Environment.ProcessorCount * timeDelta.TotalMilliseconds)) * 100;
                    info.LastCpuValue = Math.Round(Math.Clamp(cpu, 0.0, 100.0), 1);
                }

                info.LastTotalProcessorTime = currentProcessorTime;
                info.LastCpuCheckTime = now;
            }
        }
        catch (Exception)
        {
            // Occurs if process exits during sampling
        }
    }

    private void HandleProcessExit(string gameId)
    {
        if (!_trackedProcesses.TryGetValue(gameId, out var info)) return;

        // Synchronize on info to prevent concurrent execution from Exited event and Poll Timer
        lock (info)
        {
            if (!info.IsRunning) return;
            info.IsRunning = false;
        }

        int exitCode = -1;
        try
        {
            info.Process.Refresh();
            exitCode = info.Process.ExitCode;
        }
        catch { }

        info.ExitCode = exitCode;
        var duration = DateTime.UtcNow - info.StartTime;

        _logger.LogInformation("Process exited for GameId: '{GameId}' (ExitCode: {ExitCode}, Duration: {Duration:hh\\:mm\\:ss})",
            gameId, exitCode, duration);

        // Crash Detection rules
        bool hasCrashed = false;
        string crashReason = "";

        if (exitCode != 0 && exitCode != -1)
        {
            hasCrashed = true;
            crashReason = $"Unexpected exit code: {exitCode}";
        }
        else if (duration.TotalSeconds < 5)
        {
            hasCrashed = true;
            crashReason = "Application closed immediately after launching (startup failure).";
        }

        if (hasCrashed)
        {
            IncrementCrashes();
        }

        info.HasCrashed = hasCrashed;
        info.CrashReason = crashReason;

        // Fire event via GameLauncherService
        _ = Task.Run(async () =>
        {
            try
            {
                var launcher = _serviceProvider.GetService<IGameLauncherService>();
                if (launcher != null)
                {
                    if (hasCrashed)
                    {
                        launcher.RaiseGameCrashed(gameId, info.Name, exitCode, crashReason);

                        // Delegate recovery to RecoveryService
                        var recovery = _serviceProvider.GetService<ILauncherRecoveryService>();
                        if (recovery != null)
                        {
                            await recovery.HandleGameCrashAsync(gameId, info.Options, exitCode);
                        }
                    }
                    else
                    {
                        launcher.RaiseGameExited(gameId, info.Name, exitCode, duration);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling process exit events.");
            }
        });
    }

    private ProcessStatistics ToStatistics(TrackedProcessInfo info)
    {
        var duration = DateTime.UtcNow - info.StartTime;
        return new ProcessStatistics
        {
            Pid = info.LastKnownPid,
            GameId = info.GameId,
            Name = info.Name,
            IsRunning = info.IsRunning,
            CpuUsagePercentage = info.IsRunning ? info.LastCpuValue : 0.0,
            RamUsageMb = info.IsRunning ? info.LastRamValue : 0.0,
            RunningDuration = info.IsRunning ? duration : TimeSpan.Zero,
            ExitCode = info.IsRunning ? null : info.ExitCode,
            HasCrashed = info.HasCrashed,
            CrashReason = info.CrashReason
        };
    }

    public void Dispose()
    {
        _monitoringTimer.Dispose();
        foreach (var info in _trackedProcesses.Values)
        {
            try
            {
                info.Process.Dispose();
            }
            catch { }
        }
        _trackedProcesses.Clear();
    }
}
