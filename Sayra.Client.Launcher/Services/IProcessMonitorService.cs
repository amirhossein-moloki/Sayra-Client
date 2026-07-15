using System;
using System.Collections.Generic;
using System.Diagnostics;
using Sayra.Client.Launcher.Models;

namespace Sayra.Client.Launcher.Services;

public interface IProcessMonitorService
{
    void RegisterProcess(string gameId, Process process, LaunchOptions options);
    void UnregisterProcess(string gameId);
    IEnumerable<ProcessStatistics> GetRunningProcesses();
    ProcessStatistics? GetProcessStatistics(string gameId);
    bool IsGameRunning(string gameId);

    int GetTotalLaunches();
    int GetTotalCrashes();
    int GetTotalRestarts();
    void IncrementLaunches();
    void IncrementCrashes();
    void IncrementRestarts();
}
