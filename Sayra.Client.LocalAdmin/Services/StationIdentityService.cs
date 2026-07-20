using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.LocalAdmin.Services
{
    public class StationIdentityService : IStationIdentityService
    {
        private readonly IClientConfigurationService _configService;
        private readonly ILogger<StationIdentityService>? _logger;
        private StationIdentity? _cachedIdentity;
        private readonly object _lock = new();

        public StationIdentityService(IClientConfigurationService configService, ILogger<StationIdentityService>? logger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger;
        }

        public StationIdentity GetIdentity()
        {
            lock (_lock)
            {
                if (_cachedIdentity != null)
                {
                    return _cachedIdentity;
                }

                _logger?.LogInformation("Resolving Station Identity...");

                // Synchronously get configuration to avoid deadlock issues in UI thread
                ClientConfiguration config;
                try
                {
                    config = Task.Run(async () => await _configService.GetConfigurationAsync()).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load client configuration for station identity, using defaults.");
                    config = new ClientConfiguration();
                }

                bool mustSaveConfig = false;

                if (string.IsNullOrWhiteSpace(config.StationId))
                {
                    config.StationId = Guid.NewGuid().ToString();
                    mustSaveConfig = true;
                }

                if (string.IsNullOrWhiteSpace(config.ClientId))
                {
                    config.ClientId = Guid.NewGuid().ToString();
                    mustSaveConfig = true;
                }

                if (mustSaveConfig)
                {
                    try
                    {
                        Task.Run(async () => await _configService.SaveConfigurationAsync(config)).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to save generated StationId or ClientId to configuration.");
                    }
                }

                string hostname = Dns.GetHostName();
                string machineName = Environment.MachineName;

                // Query networking info
                string localIp = "127.0.0.1";
                string macAddress = "00:00:00:00:00:00";

                try
                {
                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        {
                            var props = ni.GetIPProperties();
                            foreach (var ip in props.UnicastAddresses)
                            {
                                if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    localIp = ip.Address.ToString();
                                    break;
                                }
                            }

                            string rawMac = ni.GetPhysicalAddress().ToString();
                            if (!string.IsNullOrEmpty(rawMac))
                            {
                                if (rawMac.Length == 12)
                                {
                                    var parts = new string[6];
                                    for (int i = 0; i < 6; i++) parts[i] = rawMac.Substring(i * 2, 2);
                                    macAddress = string.Join(":", parts);
                                }
                                else
                                {
                                    macAddress = rawMac;
                                }
                            }
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to query network interfaces for station identity.");
                }

                string envInfo = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

                string? configuredName = config.StationName;
                string generatedName = $"SAYRA-{machineName}";

                string resolvedName = !string.IsNullOrWhiteSpace(configuredName)
                    ? configuredName
                    : (!string.IsNullOrWhiteSpace(machineName) ? machineName : generatedName);

                _cachedIdentity = new StationIdentity
                {
                    MachineName = machineName,
                    ConfiguredStationName = configuredName,
                    StationId = config.StationId,
                    ClientId = config.ClientId,
                    MacAddress = macAddress,
                    LocalIPv4 = localIp,
                    CurrentHostname = hostname,
                    EnvironmentInformation = envInfo,
                    ResolvedStationName = resolvedName
                };

                _logger?.LogInformation("Station Identity resolved: {ResolvedName} (ID: {StationId})",
                    _cachedIdentity.ResolvedStationName, _cachedIdentity.StationId);

                return _cachedIdentity;
            }
        }
    }
}
