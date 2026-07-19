using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Ipc;
using Sayra.Client.Shared.Models;
using Sayra.Client.Launcher.Services;
using System.Diagnostics;

namespace SayraClient.Services;

public class WhitelistingService : BackgroundService
{
    private readonly ILogger<WhitelistingService> _logger;
    private readonly IpcServer _ipcServer;
    private readonly IProcessMonitorService _processMonitor;

    private readonly HashSet<string> _systemWhitelist = new()
    {
        "idle", "system", "registry", "smss", "csrss", "wininit", "services", "lsass", "svchost",
        "fontdrvhost", "winlogon", "dwm", "spoolsv", "sihost", "taskhostw", "explorer", "shellexperiencehost",
        "searchui", "searchhost", "startmenuexperiencehost", "ctfmon", "conhost", "dllhost", "runtimebroker",
        "sayraclient", "sayra.client.ui", "sayra.client.guardian", "msmpeng", "nissrv",
        "searchindexer", "wmiprvse", "audiodg", "smartscreen", "backgroundtaskhost", "applicationframehost"
    };

    public WhitelistingService(
        ILogger<WhitelistingService> logger,
        IpcServer ipcServer,
        IProcessMonitorService processMonitor)
    {
        _logger = logger;
        _ipcServer = ipcServer;
        _processMonitor = processMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Process Whitelisting Service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckRunningProcessesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during process whitelisting check.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task CheckRunningProcessesAsync()
    {
        var runningGames = _processMonitor.GetRunningProcesses().Select(g => g.Name.ToLowerInvariant()).ToHashSet();
        var processes = Process.GetProcesses();

        foreach (var proc in processes)
        {
            try
            {
                string procName = proc.ProcessName.ToLowerInvariant();

                if (_systemWhitelist.Contains(procName) || runningGames.Contains(procName))
                {
                    continue;
                }

                _logger.LogWarning("Unauthorized process detected: {ProcessName} (PID: {PID}). Terminating...", proc.ProcessName, proc.Id);

                await _ipcServer.BroadcastEventAsync(IpcMessageType.PROCESS_BLOCKED, new SecurityEventPayload
                {
                    EventType = "UNAUTHORIZED_PROCESS",
                    Severity = "Medium",
                    Description = $"Unauthorized process {proc.ProcessName} was detected and terminated.",
                    Details = $"PID: {proc.Id}"
                });

                proc.Kill(true);
            }
            catch (Exception)
            {
                // Might fail for system processes we don't have access to, which is fine
            }
        }
    }
}
