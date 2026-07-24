using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services.Windows;

public class TaskSchedulerFallbackService : SupervisedBackgroundService
{
    private const string TaskName = "SAYRA_Client_KeepAlive";

    public TaskSchedulerFallbackService(ILogger<TaskSchedulerFallbackService> logger, IServiceHealthMonitor healthMonitor)
        : base(logger, healthMonitor, "TaskSchedulerFallbackService")
    {
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TaskSchedulerFallbackService starting keep-alive registration check...");

        if (OperatingSystem.IsWindows())
        {
            try
            {
                await EnsureKeepAliveTaskRegisteredAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check or register SAYRA Client keep-alive scheduled task.");
            }
        }
        else
        {
            _logger.LogWarning("TaskSchedulerFallbackService scheduled tasks are only fully supported on Windows.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            _healthMonitor.ReportHeartbeat(_serviceName);
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken); // Check periodically every hour
        }
    }

    private async Task EnsureKeepAliveTaskRegisteredAsync(CancellationToken ct)
    {
        bool taskExists = false;

        try
        {
            var queryStartInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/query /tn \"{TaskName}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var queryProc = Process.Start(queryStartInfo);
            if (queryProc != null)
            {
                await queryProc.WaitForExitAsync(ct);
                taskExists = queryProc.ExitCode == 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query scheduled tasks via schtasks.exe.");
        }

        if (!taskExists)
        {
            _logger.LogInformation("Scheduled task '{TaskName}' not found. Registering high-privilege keep-alive scheduled task...", TaskName);

            try
            {
                // Retrieve the path of the currently executing agent binary
                string agentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "SayraClient.exe";

                // Register task to run under SYSTEM account on computer boot (/sc onstart) and logon (/sc onlogon)
                var createStartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/create /tn \"{TaskName}\" /tr \"\\\"{agentExePath}\\\"\" /sc onstart /ru \"SYSTEM\" /f",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var createProc = Process.Start(createStartInfo);
                if (createProc != null)
                {
                    await createProc.WaitForExitAsync(ct);
                    string output = await createProc.StandardOutput.ReadToEndAsync(ct);
                    string error = await createProc.StandardError.ReadToEndAsync(ct);

                    if (createProc.ExitCode == 0)
                    {
                        _logger.LogInformation("Successfully registered keep-alive scheduled task '{TaskName}' under SYSTEM context.", TaskName);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to register scheduled task. ExitCode: {ExitCode}. Output: {Output}, Error: {Error}", createProc.ExitCode, output, error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during scheduling of the keep-alive task.");
            }
        }
        else
        {
            _logger.LogInformation("SAYRA Client keep-alive scheduled task '{TaskName}' is already successfully registered.", TaskName);
        }
    }
}
