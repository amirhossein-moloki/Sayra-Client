using Microsoft.Extensions.Logging;
using SayraClient.Models;
using SayraClient.Services;
using System.Text.Json;

namespace SayraClient.Commands;

public class SessionCommandHandler : ICommandHandler
{
    private readonly ILogger<SessionCommandHandler> _logger;
    private readonly SessionManager _sessionManager;

    public SessionCommandHandler(ILogger<SessionCommandHandler> logger, SessionManager sessionManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
    }

    public bool CanHandle(string action)
    {
        return action.ToUpper() switch
        {
            "START_SESSION" or "STOP_SESSION" or "PAUSE_SESSION" or "RESUME_SESSION" => true,
            _ => false
        };
    }

    public async Task<ExecutionResult?> HandleAsync(CommandModel command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing session action: {action}", command.Action);

        var result = command.Action.ToUpper() switch
        {
            "START_SESSION" => HandleStartSession(command),
            "STOP_SESSION" => HandleStopSession(command.PcId),
            "PAUSE_SESSION" => HandlePauseSession(command.PcId),
            "RESUME_SESSION" => HandleResumeSession(command.PcId),
            _ => ExecutionResult.Error(command.Action, "Unsupported session action", command.PcId)
        };

        if (result != null)
        {
            _logger.LogInformation("Session action {action} completed with status: {status}", command.Action, result.Status);
        }

        return result;
    }

    private ExecutionResult HandleStartSession(CommandModel command)
    {
        try
        {
            if (command.Payload == null)
            {
                return ExecutionResult.Error("START_SESSION", "Missing session payload", command.PcId);
            }

            var json = JsonSerializer.Serialize(command.Payload);
            var session = JsonSerializer.Deserialize<SessionModel>(json);

            if (session == null)
            {
                return ExecutionResult.Error("START_SESSION", "Invalid session payload", command.PcId);
            }

            return _sessionManager.StartSession(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting session");
            return ExecutionResult.Error("START_SESSION", ex.Message, command.PcId);
        }
    }

    private ExecutionResult HandleStopSession(string pcId)
    {
        return _sessionManager.StopSession(pcId);
    }

    private ExecutionResult HandlePauseSession(string pcId)
    {
        return _sessionManager.PauseSession(pcId);
    }

    private ExecutionResult HandleResumeSession(string pcId)
    {
        return _sessionManager.ResumeSession(pcId);
    }
}
