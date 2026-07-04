using Microsoft.Extensions.Logging;
using SayraClient.Commands;

namespace SayraClient;

public class MessageHandler
{
    private readonly ILogger<MessageHandler> _logger;
    private readonly CommandParser _commandParser;
    private readonly CommandRouter _commandRouter;
    private readonly Services.SecureMessageValidator _messageValidator;
    private readonly Services.SecureTransportLayer _transportLayer;
    private readonly Services.AuthManager _authManager;

    public MessageHandler(
        ILogger<MessageHandler> logger,
        CommandParser commandParser,
        CommandRouter commandRouter,
        Services.SecureMessageValidator messageValidator,
        Services.SecureTransportLayer transportLayer,
        Services.AuthManager authManager)
    {
        _logger = logger;
        _commandParser = commandParser;
        _commandRouter = commandRouter;
        _messageValidator = messageValidator;
        _transportLayer = transportLayer;
        _authManager = authManager;
    }

    public async Task HandleMessageAsync(string messageJson, NetworkManager networkManager, CancellationToken cancellationToken)
    {
        try
        {
            string? unwrappedJson = _transportLayer.Unwrap(messageJson);
            if (unwrappedJson == null) return;

            // Check if it's a handshake message first
            using (var doc = System.Text.Json.JsonDocument.Parse(unwrappedJson))
            {
                _logger.LogDebug("Processing unwrapped message: {json}", unwrappedJson);
                if (doc.RootElement.TryGetProperty("type", out var typeProp))
                {
                    string type = typeProp.GetString() ?? "";
                    if (type.Equals("AUTH_CHALLENGE", StringComparison.OrdinalIgnoreCase))
                    {
                        var challenge = System.Text.Json.JsonSerializer.Deserialize<Models.AuthChallengeModel>(unwrappedJson);
                        if (challenge != null)
                        {
                            var response = await _authManager.HandleChallengeAsync(challenge);
                            if (response != null)
                            {
                                await networkManager.SendMessageAsync(response, cancellationToken);
                            }
                        }
                        return;
                    }
                    else if (type.Equals("AUTH_STATUS", StringComparison.OrdinalIgnoreCase))
                    {
                        var status = System.Text.Json.JsonSerializer.Deserialize<Models.AuthStatusModel>(unwrappedJson);
                        if (status != null)
                        {
                            _authManager.HandleAuthStatus(status);
                            if (status.Status == "SUCCESS")
                            {
                                // Notify of current state upon successful authentication
                                _ = networkManager.SendStateSyncAsync(cancellationToken);
                            }
                        }
                        return;
                    }
                }
            }

            var command = _commandParser.Parse(unwrappedJson);

            if (!_messageValidator.Validate(command))
            {
                return;
            }

            _logger.LogInformation("Handling message type: {type}", command!.Type);

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
