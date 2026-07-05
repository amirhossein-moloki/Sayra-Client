using System.Text.Json;
using SayraClient.Models;
using Microsoft.Extensions.Logging;

namespace SayraClient.Commands;

public class CommandParser
{
    private readonly ILogger<CommandParser> _logger;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CommandParser(ILogger<CommandParser> logger)
    {
        _logger = logger;
    }

    public CommandModel? Parse(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var command = JsonSerializer.Deserialize<CommandModel>(json, Options);
            if (command == null) return null;

            // If action is present, it's likely a COMMAND type message aligned with SendCommandRequest
            if (!string.IsNullOrWhiteSpace(command.Action) && string.IsNullOrWhiteSpace(command.Type))
            {
                command.Type = "COMMAND";
            }

            if (string.IsNullOrWhiteSpace(command.Type))
            {
                _logger.LogWarning("Command missing 'type' and 'action' field.");
                return null;
            }

            return command;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse command JSON.");
            return null;
        }
    }
}
