using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SayraClient.Models;

namespace SayraClient.Commands;

using SayraClient.Services;

public class SystemCommandHandler : ICommandHandler
{
    private readonly ILogger<SystemCommandHandler> _logger;
    private readonly DiagnosticsService _diagnosticsService;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    public SystemCommandHandler(ILogger<SystemCommandHandler> logger, DiagnosticsService diagnosticsService)
    {
        _logger = logger;
        _diagnosticsService = diagnosticsService;
    }

    public bool CanHandle(string action)
    {
        return action.ToUpper() switch
        {
            "LOCK_PC" or "UNLOCK_PC" or "PING" or "GET_DIAGNOSTICS" => true,
            _ => false
        };
    }

    public async Task<ExecutionResult?> HandleAsync(CommandModel command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing action: {action}", command.Action);

        var result = command.Action.ToUpper() switch
        {
            "PING" => new ExecutionResult { Type = "PONG", Action = null!, Status = null!, Message = null! },
            "GET_DIAGNOSTICS" => HandleGetDiagnostics(),
            "LOCK_PC" => HandleLockPc(),
            "UNLOCK_PC" => HandleUnlockPc(),
            _ => ExecutionResult.Error(command.Action, "Unsupported action")
        };

        if (result != null && result.Type != "PONG")
        {
            _logger.LogInformation("System action {action} completed with status: {status}", command.Action, result.Status);
        }

        return result;
    }

    private ExecutionResult HandleLockPc()
    {
        try
        {
            _logger.LogInformation("Attempting to lock workstation...");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (LockWorkStation())
                {
                    return ExecutionResult.Success("LOCK_PC", "PC locked successfully");
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    return ExecutionResult.Error("LOCK_PC", $"Failed to lock PC. Win32 Error: {error}");
                }
            }
            else
            {
                _logger.LogWarning("LOCK_PC called on non-Windows platform.");
                return ExecutionResult.Success("LOCK_PC", "PC lock simulated (non-Windows)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing LOCK_PC");
            return ExecutionResult.Error("LOCK_PC", ex.Message);
        }
    }

    private ExecutionResult HandleUnlockPc()
    {
        _logger.LogInformation("UNLOCK_PC received (placeholder)");
        return ExecutionResult.Success("UNLOCK_PC", "PC unlock requested (placeholder)");
    }

    private ExecutionResult HandleGetDiagnostics()
    {
        try
        {
            var data = _diagnosticsService.GetDiagnostics();
            return new ExecutionResult
            {
                Type = "EXECUTION_RESULT",
                Action = "GET_DIAGNOSTICS",
                Status = "SUCCESS",
                Data = data
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error gathering diagnostics");
            return ExecutionResult.Error("GET_DIAGNOSTICS", ex.Message);
        }
    }
}
