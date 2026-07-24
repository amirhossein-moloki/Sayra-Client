using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sayra.Client.Configuration.Models;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.Configuration.Synchronization;

public class ConfigurationDeltaEngine
{
    private readonly ILogger<ConfigurationDeltaEngine>? _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public ConfigurationDeltaEngine(ILogger<ConfigurationDeltaEngine>? logger = null)
    {
        _logger = logger;
    }

    public string ComputeSectionHash(object section)
    {
        if (section == null) return string.Empty;
        string json = JsonSerializer.Serialize(section, SerializerOptions);
        using (var sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }

    public Dictionary<string, string> ComputeAllSectionHashes(ClientConfiguration config)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (config == null) return hashes;

        hashes["ServerDiscovery"] = ComputeSectionHash(config.ServerDiscovery);
        hashes["GameLibrary"] = ComputeSectionHash(config.GameLibrary);
        hashes["ScannerPaths"] = ComputeSectionHash(config.ScannerPaths);
        hashes["LocalPreferences"] = ComputeSectionHash(config.LocalPreferences);
        hashes["StationName"] = ComputeSectionHash(config.StationName ?? string.Empty);
        hashes["StationId"] = ComputeSectionHash(config.StationId ?? string.Empty);
        hashes["ClientId"] = ComputeSectionHash(config.ClientId ?? string.Empty);

        return hashes;
    }

    public bool ApplyDeltas(ClientConfiguration localConfig, List<ConfigurationDelta> deltas, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (localConfig == null)
        {
            errorMessage = "Local configuration object is null.";
            return false;
        }

        if (deltas == null || deltas.Count == 0)
        {
            _logger?.LogInformation("No deltas provided. System is already up to date.");
            return true;
        }

        // Compute current section hashes before modifying
        var currentHashes = ComputeAllSectionHashes(localConfig);

        try
        {
            foreach (var delta in deltas)
            {
                string section = delta.Section;
                _logger?.LogInformation($"Applying delta for section '{section}'...");

                // Validate OldHash to detect local modifications/conflicts
                if (currentHashes.TryGetValue(section, out var currentHash))
                {
                    if (!string.IsNullOrEmpty(delta.OldHash) && !string.Equals(currentHash, delta.OldHash, StringComparison.OrdinalIgnoreCase))
                    {
                        errorMessage = $"Delta mismatch on section '{section}': local hash '{currentHash}' does not match expected old hash '{delta.OldHash}'. Falling back to Full Sync.";
                        _logger?.LogWarning(errorMessage);
                        return false;
                    }
                }

                // Apply the patch payload to the specific section
                switch (section)
                {
                    case "ServerDiscovery":
                        var newDisc = JsonSerializer.Deserialize<ServerDiscoverySettings>(delta.Patch, SerializerOptions);
                        if (newDisc == null) throw new JsonException("Deserialized ServerDiscovery is null.");
                        localConfig.ServerDiscovery = newDisc;
                        break;

                    case "GameLibrary":
                        var newLib = JsonSerializer.Deserialize<GameLibrarySettings>(delta.Patch, SerializerOptions);
                        if (newLib == null) throw new JsonException("Deserialized GameLibrary is null.");
                        localConfig.GameLibrary = newLib;
                        break;

                    case "ScannerPaths":
                        var newPaths = JsonSerializer.Deserialize<List<string>>(delta.Patch, SerializerOptions);
                        if (newPaths == null) throw new JsonException("Deserialized ScannerPaths is null.");
                        localConfig.ScannerPaths = newPaths;
                        break;

                    case "LocalPreferences":
                        var newPref = JsonSerializer.Deserialize<LocalPreferencesSettings>(delta.Patch, SerializerOptions);
                        if (newPref == null) throw new JsonException("Deserialized LocalPreferences is null.");
                        localConfig.LocalPreferences = newPref;
                        break;

                    case "StationName":
                        localConfig.StationName = JsonSerializer.Deserialize<string>(delta.Patch, SerializerOptions);
                        break;

                    case "StationId":
                        localConfig.StationId = JsonSerializer.Deserialize<string>(delta.Patch, SerializerOptions);
                        break;

                    case "ClientId":
                        localConfig.ClientId = JsonSerializer.Deserialize<string>(delta.Patch, SerializerOptions);
                        break;

                    default:
                        errorMessage = $"Unknown section '{section}' in delta. Falling back to Full Sync.";
                        _logger?.LogWarning(errorMessage);
                        return false;
                }

                // Verify the newly computed hash matches delta's NewHash
                string postApplyHash = section switch
                {
                    "ServerDiscovery" => ComputeSectionHash(localConfig.ServerDiscovery),
                    "GameLibrary" => ComputeSectionHash(localConfig.GameLibrary),
                    "ScannerPaths" => ComputeSectionHash(localConfig.ScannerPaths),
                    "LocalPreferences" => ComputeSectionHash(localConfig.LocalPreferences),
                    "StationName" => ComputeSectionHash(localConfig.StationName ?? string.Empty),
                    "StationId" => ComputeSectionHash(localConfig.StationId ?? string.Empty),
                    "ClientId" => ComputeSectionHash(localConfig.ClientId ?? string.Empty),
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(delta.NewHash) && !string.Equals(postApplyHash, delta.NewHash, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"Post-apply verification failed for section '{section}': computed hash '{postApplyHash}' does not match expected new hash '{delta.NewHash}'.";
                    _logger?.LogError(errorMessage);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Error occurred while applying configuration deltas: {ex.Message}";
            _logger?.LogError(ex, errorMessage);
            return false;
        }
    }
}
