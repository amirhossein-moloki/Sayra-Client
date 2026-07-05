using Microsoft.Extensions.Logging;
using SayraClient.Commands;
using SayraClient.Models;
using SayraClient.Services;
using System.Text.Json;

namespace SayraClient;

public class MessageHandler
{
    private readonly ILogger<MessageHandler> _logger;
    private readonly CommandParser _commandParser;
    private readonly CommandRouter _commandRouter;
    private readonly SecureMessageValidator _messageValidator;
    private readonly SecureTransportLayer _transportLayer;
    private readonly AuthManager _authManager;
    private readonly ClientStateManager _stateManager;

    public MessageHandler(
        ILogger<MessageHandler> logger,
        CommandParser commandParser,
        CommandRouter commandRouter,
        SecureMessageValidator messageValidator,
        SecureTransportLayer transportLayer,
        AuthManager authManager,
        ClientStateManager stateManager)
    {
        _logger = logger;
        _commandParser = commandParser;
        _commandRouter = commandRouter;
        _messageValidator = messageValidator;
        _transportLayer = transportLayer;
        _authManager = authManager;
        _stateManager = stateManager;
    }

    public async Task HandleMessageAsync(string messageJson, TcpClientManager tcpClientManager, CancellationToken cancellationToken)
    {
        try
        {
            string? unwrappedJson = _transportLayer.Unwrap(messageJson);
            if (unwrappedJson == null) return;

            using (var doc = JsonDocument.Parse(unwrappedJson))
            {
                if (doc.RootElement.TryGetProperty("type", out var typeProp))
                {
                    string type = typeProp.GetString() ?? "";

                    if (type.Equals("AUTH_CHALLENGE", StringComparison.OrdinalIgnoreCase))
                    {
                        var challenge = JsonSerializer.Deserialize<AuthChallengeModel>(unwrappedJson);
                        if (challenge != null)
                        {
                            var response = await _authManager.HandleChallengeAsync(challenge);
                            if (response != null)
                            {
                                await tcpClientManager.SendMessageAsync(response, cancellationToken);
                            }
                        }
                        return;
                    }
                    else if (type.Equals("AUTH_STATUS", StringComparison.OrdinalIgnoreCase))
                    {
                        var status = JsonSerializer.Deserialize<AuthStatusModel>(unwrappedJson);
                        if (status != null)
                        {
                            _authManager.HandleAuthStatus(status);
                            if (status.Status == "SUCCESS")
                            {
                                _stateManager.TransitionTo(ClientState.READY);
                                await tcpClientManager.SendStateSyncAsync(cancellationToken);
                            }
                            else
                            {
                                _stateManager.TransitionTo(ClientState.AUTHENTICATING);
                            }
                        }
                        return;
                    }
                }
            }

            // For any other message, check if we are in a ready state
            if (!_stateManager.IsReady())
            {
                _logger.LogWarning("Received message while not in READY state. Current state: {state}", _stateManager.CurrentState);
                return;
            }

            var command = _commandParser.Parse(unwrappedJson);
            if (!_messageValidator.Validate(command)) return;

            _logger.LogInformation("Handling message type: {type}", command!.Type);

            switch (command.Type.ToUpper())
            {
                case "COMMAND":
                    var result = await _commandRouter.RouteAsync(command, cancellationToken);
                    if (result != null)
                    {
                        // Ensure result has PC ID and Command ID if possible
                        result.PcId = command.PcId;
                        await tcpClientManager.SendMessageAsync(result, cancellationToken);
                    }
                    break;

                case "PING":
                    await tcpClientManager.SendMessageAsync(new { type = "PONG" }, cancellationToken);
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
