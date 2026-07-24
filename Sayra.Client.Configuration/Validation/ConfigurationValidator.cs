using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.Configuration.Validation;

public class ConfigurationValidator
{
    private static readonly JsonSerializerOptions StrictOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public bool Validate(string payload, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(payload))
        {
            errorMessage = "Payload is null or empty.";
            return false;
        }

        try
        {
            // Deserialize with strict options to reject unexpected/unknown fields and handle malformed json
            var config = JsonSerializer.Deserialize<ClientConfiguration>(payload, StrictOptions);
            if (config == null)
            {
                errorMessage = "Deserialized configuration is null.";
                return false;
            }

            return ValidateConfigObject(config, out errorMessage);
        }
        catch (JsonException ex)
        {
            errorMessage = $"JSON validation/schema failed: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Unexpected configuration validation error: {ex.Message}";
            return false;
        }
    }

    public bool ValidateConfigObject(ClientConfiguration config, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (config.ServerDiscovery == null)
        {
            errorMessage = "ServerDiscovery settings are missing.";
            return false;
        }

        if (config.ServerDiscovery.UdpPort <= 0 || config.ServerDiscovery.UdpPort > 65535)
        {
            errorMessage = $"ServerDiscovery UdpPort '{config.ServerDiscovery.UdpPort}' is out of range (1-65535).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.ServerDiscovery.ServerIp))
        {
            errorMessage = "ServerDiscovery ServerIp must not be empty.";
            return false;
        }

        if (config.GameLibrary == null)
        {
            errorMessage = "GameLibrary settings are missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.GameLibrary.LibraryPath))
        {
            errorMessage = "GameLibrary LibraryPath must not be empty.";
            return false;
        }

        if (config.LocalPreferences == null)
        {
            errorMessage = "LocalPreferences settings are missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.LocalPreferences.Theme))
        {
            errorMessage = "LocalPreferences Theme must not be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.LocalPreferences.Language))
        {
            errorMessage = "LocalPreferences Language must not be empty.";
            return false;
        }

        return true;
    }
}
