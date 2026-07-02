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
            var command = JsonSerializer.Deserialize<CommandModel>(json, Options);
            if (command == null) return null;

            if (string.IsNullOrWhiteSpace(command.Type))
            {
                _logger.LogWarning("Command missing 'type' field.");
                return null;
            }

            if (command.Type.ToUpper() == "COMMAND" && string.IsNullOrWhiteSpace(command.Action))
            {
                _logger.LogWarning("COMMAND message missing 'action' field.");
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
