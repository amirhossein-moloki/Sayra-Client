using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SayraClient.Models;

namespace SayraClient.Commands;

public class SystemCommandHandler : ICommandHandler
{
    private readonly ILogger<SystemCommandHandler> _logger;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    public SystemCommandHandler(ILogger<SystemCommandHandler> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(string action)
    {
        return action.ToUpper() switch
        {
            "LOCK_PC" or "UNLOCK_PC" or "PING" => true,
            _ => false
        };
    }

    public async Task<ExecutionResult?> HandleAsync(CommandModel command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing action: {action}", command.Action);

        return command.Action.ToUpper() switch
        {
            "PING" => new ExecutionResult { Type = "PONG", Action = null!, Status = null!, Message = null! },
            "LOCK_PC" => HandleLockPc(),
            "UNLOCK_PC" => HandleUnlockPc(),
            _ => ExecutionResult.Error(command.Action, "Unsupported action")
        };
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
}
