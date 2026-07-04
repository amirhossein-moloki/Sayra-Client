using Microsoft.Extensions.Logging;
using SayraClient.Models;
using System.Text.Json;

namespace SayraClient.Services;

public class SecureMessageValidator
{
    private readonly ILogger<SecureMessageValidator> _logger;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "COMMAND", "PING", "HEARTBEAT", "AUTH_CHALLENGE", "AUTH_STATUS"
    };

    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "LOCK_PC", "UNLOCK_PC", "PING", "RUN_APP", "KILL_APP", "LIST_PROCESSES",
        "START_SESSION", "STOP_SESSION", "PAUSE_SESSION", "RESUME_SESSION"
    };

    public SecureMessageValidator(ILogger<SecureMessageValidator> logger)
    {
        _logger = logger;
    }

    public bool Validate(CommandModel? command)
    {
        if (command == null)
        {
            _logger.LogWarning("Validation failed: Command is null.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(command.Type))
        {
            _logger.LogWarning("Validation failed: Type is missing.");
            return false;
        }

        if (!AllowedTypes.Contains(command.Type))
        {
            _logger.LogWarning("Validation failed: Unknown type '{Type}'.", command.Type);
            return false;
        }

        if (command.Type.Equals("COMMAND", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(command.Action))
            {
                _logger.LogWarning("Validation failed: Action is missing for COMMAND type.");
                return false;
            }

            if (!AllowedActions.Contains(command.Action))
            {
                _logger.LogWarning("Validation failed: Unauthorized action '{Action}'.", command.Action);
                return false;
            }
        }

        return true;
    }

    public bool IsJsonValid(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            _logger.LogWarning("Malformed JSON received.");
            return false;
        }
    }
}
