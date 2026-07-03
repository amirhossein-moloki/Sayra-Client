using System.Text.Json;
using Microsoft.Extensions.Logging;
using SayraClient.Models;
using SayraClient.Services;

namespace SayraClient.Commands;

public class AppCommandHandler : ICommandHandler
{
    private readonly GameLauncher _gameLauncher;
    private readonly ProcessManager _processManager;
    private readonly ProcessMonitor _processMonitor;
    private readonly ILogger<AppCommandHandler> _logger;

    public AppCommandHandler(
        GameLauncher gameLauncher,
        ProcessManager processManager,
        ProcessMonitor processMonitor,
        ILogger<AppCommandHandler> logger)
    {
        _gameLauncher = gameLauncher;
        _processManager = processManager;
        _processMonitor = processMonitor;
        _logger = logger;
    }

    public bool CanHandle(string action)
    {
        return action.ToUpper() switch
        {
            "RUN_APP" or "KILL_APP" or "LIST_PROCESSES" => true,
            _ => false
        };
    }

    public async Task<ExecutionResult?> HandleAsync(CommandModel command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling action: {action}", command.Action);

        return command.Action.ToUpper() switch
        {
            "RUN_APP" => HandleRunApp(command.Payload),
            "KILL_APP" => HandleKillApp(command.Payload),
            "LIST_PROCESSES" => HandleListProcesses(),
            _ => ExecutionResult.Error(command.Action, "Unsupported action")
        };
    }

    private ExecutionResult HandleRunApp(object? payload)
    {
        try
        {
            if (payload is JsonElement element && element.TryGetProperty("path", out var pathProperty))
            {
                string path = pathProperty.GetString() ?? throw new Exception("Path is null");
                _gameLauncher.LaunchGame(path);
                return ExecutionResult.Success("RUN_APP", "Application started");
            }

            return ExecutionResult.Error("RUN_APP", "Invalid payload: missing path");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing RUN_APP");
            return ExecutionResult.Error("RUN_APP", ex.Message);
        }
    }

    private ExecutionResult HandleKillApp(object? payload)
    {
        try
        {
            if (payload is JsonElement element)
            {
                if (element.TryGetProperty("pid", out var pidProperty) && pidProperty.TryGetInt32(out int pid))
                {
                    _processManager.KillProcess(pid);
                    return ExecutionResult.Success("KILL_APP");
                }

                if (element.TryGetProperty("name", out var nameProperty))
                {
                    string name = nameProperty.GetString() ?? throw new Exception("Name is null");
                    _processManager.KillProcessByName(name);
                    return ExecutionResult.Success("KILL_APP");
                }
            }

            return ExecutionResult.Error("KILL_APP", "Invalid payload: missing pid or name");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing KILL_APP");
            return ExecutionResult.Error("KILL_APP", ex.Message);
        }
    }

    private ExecutionResult HandleListProcesses()
    {
        try
        {
            var processes = _processMonitor.GetRunningProcesses();
            return ExecutionResult.Success("LIST_PROCESSES", data: processes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing LIST_PROCESSES");
            return ExecutionResult.Error("LIST_PROCESSES", ex.Message);
        }
    }
}
