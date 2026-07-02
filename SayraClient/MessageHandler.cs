using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace SayraClient;

public class MessageHandler
{
    private readonly ILogger<MessageHandler> _logger;

    public MessageHandler(ILogger<MessageHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleMessageAsync(string messageJson, NetworkManager networkManager, CancellationToken cancellationToken)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var message = JsonSerializer.Deserialize<BaseMessage>(messageJson, options);
            if (message == null || string.IsNullOrEmpty(message.Type))
            {
                _logger.LogWarning("Received empty or invalid message: {json}", messageJson);
                return;
            }

            _logger.LogInformation("Handling message type: {type}", message.Type);

            switch (message.Type.ToUpper())
            {
                case "PING":
                    await networkManager.SendMessageAsync(new { type = "PONG" }, cancellationToken);
                    _logger.LogInformation("Sent PONG response.");
                    break;

                default:
                    _logger.LogWarning("Unknown message type: {type}", message.Type);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse message JSON: {json}", messageJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message.");
        }
    }

    private class BaseMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }
}
