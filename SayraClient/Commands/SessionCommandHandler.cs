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
            "STOP_SESSION" => HandleStopSession(),
            "PAUSE_SESSION" => HandlePauseSession(),
            "RESUME_SESSION" => HandleResumeSession(),
            _ => ExecutionResult.Error(command.Action, "Unsupported session action")
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
                return ExecutionResult.Error("START_SESSION", "Missing session payload");
            }

            var json = JsonSerializer.Serialize(command.Payload);
            var session = JsonSerializer.Deserialize<SessionModel>(json);

            if (session == null)
            {
                return ExecutionResult.Error("START_SESSION", "Invalid session payload");
            }

            return _sessionManager.StartSession(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting session");
            return ExecutionResult.Error("START_SESSION", ex.Message);
        }
    }

    private ExecutionResult HandleStopSession()
    {
        return _sessionManager.StopSession();
    }

    private ExecutionResult HandlePauseSession()
    {
        return _sessionManager.PauseSession();
    }

    private ExecutionResult HandleResumeSession()
    {
        return _sessionManager.ResumeSession();
    }
}
