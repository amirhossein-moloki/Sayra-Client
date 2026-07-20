using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SayraClient.Models;
using SayraClient.Services;

namespace SayraClient.Commands;

public class SystemCommandHandler : ICommandHandler
{
    private readonly ILogger<SystemCommandHandler> _logger;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly IPowerManagementService _powerManagementService;

    public SystemCommandHandler(
        ILogger<SystemCommandHandler> logger,
        DiagnosticsService diagnosticsService,
        IPowerManagementService powerManagementService)
    {
        _logger = logger;
        _diagnosticsService = diagnosticsService;
        _powerManagementService = powerManagementService;
    }

    public bool CanHandle(string action)
    {
        return action.ToUpper() switch
        {
            "LOCK_PC" or "UNLOCK_PC" or "PING" or "GET_DIAGNOSTICS" or "RESTART_PC" or "SHUTDOWN_PC" or "LOGOFF_PC" => true,
            _ => false
        };
    }

    public async Task<ExecutionResult?> HandleAsync(CommandModel command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing action: {action}", command.Action);

        try
        {
            switch (command.Action.ToUpper())
            {
                case "PING":
                    return ExecutionResult.Success("PING", "PONG", command.PcId);
                case "GET_DIAGNOSTICS":
                    return HandleGetDiagnostics(command.PcId);
                case "LOCK_PC":
                    await _powerManagementService.LockWorkstationAsync(cancellationToken);
                    return ExecutionResult.Success("LOCK_PC", "PC locked successfully", command.PcId);
                case "RESTART_PC":
                    await _powerManagementService.RestartAsync(cancellationToken);
                    return ExecutionResult.Success("RESTART_PC", "PC restart initiated", command.PcId);
                case "SHUTDOWN_PC":
                    await _powerManagementService.ShutdownAsync(cancellationToken);
                    return ExecutionResult.Success("SHUTDOWN_PC", "PC shutdown initiated", command.PcId);
                case "LOGOFF_PC":
                    await _powerManagementService.LogoffAsync(cancellationToken);
                    return ExecutionResult.Success("LOGOFF_PC", "PC logoff initiated", command.PcId);
                case "UNLOCK_PC":
                    _logger.LogWarning("UNLOCK_PC received (not fully implemented for production)");
                    return ExecutionResult.Error("UNLOCK_PC", "UNLOCK_PC is not fully implemented in this version", command.PcId);
                default:
                    return ExecutionResult.Error(command.Action, "Unsupported action", command.PcId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing action {action}", command.Action);
            return ExecutionResult.Error(command.Action, ex.Message, command.PcId);
        }
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
