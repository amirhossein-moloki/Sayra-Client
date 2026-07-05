using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SayraClient.Models;
using SayraClient.Services;

namespace SayraClient.Commands;

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
            "PING" => ExecutionResult.Success("PING", "PONG", command.PcId),
            "GET_DIAGNOSTICS" => HandleGetDiagnostics(command.PcId),
            "LOCK_PC" => HandleLockPc(command.PcId),
            "UNLOCK_PC" => HandleUnlockPc(command.PcId),
            _ => ExecutionResult.Error(command.Action, "Unsupported action", command.PcId)
        };

        return result;
    }

    private ExecutionResult HandleLockPc(string pcId)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogInformation("Attempting to lock workstation...");
                if (LockWorkStation())
                {
                    return ExecutionResult.Success("LOCK_PC", "PC locked successfully", pcId);
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    return ExecutionResult.Error("LOCK_PC", $"Failed to lock PC. Win32 Error: {error}", pcId);
                }
            }
            else
            {
                _logger.LogError("LOCK_PC called on non-Windows platform.");
                return ExecutionResult.Error("LOCK_PC", "LOCK_PC is only supported on Windows", pcId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing LOCK_PC");
            return ExecutionResult.Error("LOCK_PC", ex.Message, pcId);
        }
    }

    private ExecutionResult HandleUnlockPc(string pcId)
    {
        // Unlock workstation usually requires more complex session management or a credential provider
        _logger.LogWarning("UNLOCK_PC received (not fully implemented for production)");
        return ExecutionResult.Error("UNLOCK_PC", "UNLOCK_PC is not fully implemented in this version", pcId);
    }

    private ExecutionResult HandleGetDiagnostics(string pcId)
    {
        try
        {
            var data = _diagnosticsService.GetDiagnostics();
            return ExecutionResult.Success("GET_DIAGNOSTICS", System.Text.Json.JsonSerializer.Serialize(data), pcId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error gathering diagnostics");
            return ExecutionResult.Error("GET_DIAGNOSTICS", ex.Message, pcId);
        }
    }
}
