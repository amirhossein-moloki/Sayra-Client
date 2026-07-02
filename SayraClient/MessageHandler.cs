using Microsoft.Extensions.Logging;
using SayraClient.Commands;

namespace SayraClient;

public class MessageHandler
{
    private readonly ILogger<MessageHandler> _logger;
    private readonly CommandParser _commandParser;
    private readonly CommandRouter _commandRouter;

    public MessageHandler(
        ILogger<MessageHandler> logger,
        CommandParser commandParser,
        CommandRouter commandRouter)
    {
        _logger = logger;
        _commandParser = commandParser;
        _commandRouter = commandRouter;
    }

    public async Task HandleMessageAsync(string messageJson, NetworkManager networkManager, CancellationToken cancellationToken)
    {
        try
        {
            var command = _commandParser.Parse(messageJson);
            if (command == null)
            {
                _logger.LogWarning("Received empty or invalid message: {json}", messageJson);
                return;
            }

            _logger.LogInformation("Handling message type: {type}", command.Type);

            switch (command.Type.ToUpper())
            {
                case "COMMAND":
                    var result = await _commandRouter.RouteAsync(command, cancellationToken);
                    if (result != null)
                    {
                        await networkManager.SendMessageAsync(result, cancellationToken);
                    }
                    break;

                case "PING":
                    await networkManager.SendMessageAsync(new { type = "PONG" }, cancellationToken);
                    _logger.LogInformation("Sent PONG response to PING type.");
                    break;

                default:
                    _logger.LogWarning("Unknown message type: {type}", command.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message.");
        }
    }
}
