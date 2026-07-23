using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sayra.Client.OfflineQueue.Models;

namespace Sayra.Client.OfflineQueue.Serialization;

public class EventSerializer : IEventSerializer
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Serialize(ClientEvent clientEvent)
    {
        if (clientEvent == null) throw new ArgumentNullException(nameof(clientEvent));
        clientEvent.Validate();
        return JsonSerializer.Serialize(clientEvent, _jsonOptions);
    }

    public ClientEvent Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON payload cannot be null or whitespace.", nameof(json));

        var clientEvent = JsonSerializer.Deserialize<ClientEvent>(json, _jsonOptions);
        if (clientEvent == null)
        {
            throw new InvalidOperationException("Failed to deserialize JSON into ClientEvent.");
        }

        // Validate version compatibility
        if (!IsCompatible(clientEvent.EventVersion))
        {
            // Support backward-compatibility warning or custom handling
            System.Diagnostics.Debug.WriteLine($"Warning: Deserializing event with incompatible version '{clientEvent.EventVersion}'. Current supported major version is 1.");
        }

        clientEvent.Validate();
        return clientEvent;
    }

    public bool IsCompatible(string eventVersion)
    {
        if (string.IsNullOrWhiteSpace(eventVersion)) return false;

        // Supported major version is 1. If major matches, we are compatible.
        var parts = eventVersion.Split('.');
        if (parts.Length > 0 && int.TryParse(parts[0], out int major))
        {
            return major == 1;
        }

        return false;
    }
}
