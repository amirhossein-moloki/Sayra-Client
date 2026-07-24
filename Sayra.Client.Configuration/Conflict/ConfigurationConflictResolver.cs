using System;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.Configuration.Conflict;

public enum ConflictPolicy
{
    ServerWins,
    LocalWins,
    Merge
}

public class ConfigurationConflictResolver
{
    public ConflictPolicy Policy { get; set; } = ConflictPolicy.ServerWins;

    public ConfigurationConflictResolver(ConflictPolicy policy = ConflictPolicy.ServerWins)
    {
        Policy = policy;
    }

    public ClientConfiguration Resolve(ClientConfiguration localConfig, ClientConfiguration serverConfig)
    {
        if (localConfig == null) return serverConfig;
        if (serverConfig == null) return localConfig;

        switch (Policy)
        {
            case ConflictPolicy.LocalWins:
                return localConfig;

            case ConflictPolicy.ServerWins:
                // Server wins, but we MUST preserve essential local identifiers so that we don't identity-clash
                var resolvedServerWins = Clone(serverConfig);
                resolvedServerWins.ClientId = localConfig.ClientId;
                resolvedServerWins.StationId = localConfig.StationId;
                resolvedServerWins.StationName = localConfig.StationName;
                return resolvedServerWins;

            case ConflictPolicy.Merge:
                // Merge strategy:
                // Take server configuration as base, but keep specific local-only settings or overrides if they exist
                var merged = Clone(serverConfig);

                // Keep local identifiers
                merged.ClientId = localConfig.ClientId;
                merged.StationId = localConfig.StationId;
                merged.StationName = localConfig.StationName;

                // For preference theme/language, if local preferences is set, we can keep them or combine
                if (localConfig.LocalPreferences != null)
                {
                    merged.LocalPreferences ??= new LocalPreferencesSettings();
                    if (!string.IsNullOrEmpty(localConfig.LocalPreferences.Theme))
                    {
                        merged.LocalPreferences.Theme = localConfig.LocalPreferences.Theme;
                    }
                    if (!string.IsNullOrEmpty(localConfig.LocalPreferences.Language))
                    {
                        merged.LocalPreferences.Language = localConfig.LocalPreferences.Language;
                    }
                    merged.LocalPreferences.IsKioskMode = localConfig.LocalPreferences.IsKioskMode;
                }

                return merged;

            default:
                throw new NotSupportedException($"Conflict policy '{Policy}' is not supported.");
        }
    }

    private ClientConfiguration Clone(ClientConfiguration source)
    {
        // Simple and robust deep copy
        var target = new ClientConfiguration
        {
            StationId = source.StationId,
            StationName = source.StationName,
            ClientId = source.ClientId,
            ScannerPaths = new System.Collections.Generic.List<string>(source.ScannerPaths ?? new())
        };

        if (source.ServerDiscovery != null)
        {
            target.ServerDiscovery = new ServerDiscoverySettings
            {
                ServerIp = source.ServerDiscovery.ServerIp,
                UdpPort = source.ServerDiscovery.UdpPort,
                AutoDiscovery = source.ServerDiscovery.AutoDiscovery
            };
        }

        if (source.GameLibrary != null)
        {
            target.GameLibrary = new GameLibrarySettings
            {
                LibraryPath = source.GameLibrary.LibraryPath,
                AutoUpdate = source.GameLibrary.AutoUpdate
            };
        }

        if (source.LocalPreferences != null)
        {
            target.LocalPreferences = new LocalPreferencesSettings
            {
                Theme = source.LocalPreferences.Theme,
                Language = source.LocalPreferences.Language,
                IsKioskMode = source.LocalPreferences.IsKioskMode
            };
        }

        return target;
    }
}
