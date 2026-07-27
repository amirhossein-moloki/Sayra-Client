using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.RemoteOperations.Services
{
    public class NetworkPolicyManager
    {
        private readonly ILogger<NetworkPolicyManager> _logger;
        private readonly ConcurrentDictionary<string, string> _networkConfig = new();
        private readonly ConcurrentBag<string> _deniedApps = new();
        private readonly ConcurrentBag<string> _allowedApps = new();
        private readonly ConcurrentDictionary<string, string> _backupConfig = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        public NetworkPolicyManager(ILogger<NetworkPolicyManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> ApplyNetworkPolicyAsync(string action, string value, List<string> targets = null, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Applying network policy action '{Action}' with value '{Value}'", action, value);

                _backupConfig.TryAdd(action, _networkConfig.TryGetValue(action, out var oldVal) ? oldVal : null);

                switch (action.ToUpperInvariant())
                {
                    case "BANDWIDTH_LIMIT":
                        _networkConfig["BandwidthLimitKbps"] = value;
                        _logger.LogInformation("Bandwidth limit set to {Value} KB/s via traffic management abstraction.", value);
                        break;

                    case "ADAPTER_RESTRICTION":
                        _networkConfig["AdapterRestriction"] = value;
                        _logger.LogInformation("Network adapter restriction applied: {Value}", value);
                        break;

                    case "DNS_CONFIGURATION":
                        _networkConfig["DnsConfiguration"] = value;
                        _logger.LogInformation("Abstract DNS servers configured: {Value}", value);
                        break;

                    case "QOS_INTEGRATION":
                        _networkConfig["QosPriority"] = value;
                        _logger.LogInformation("QoS DSCP prioritization mapping registered: {Value}", value);
                        break;

                    case "APP_DENY_LIST":
                        _deniedApps.Clear();
                        if (targets != null)
                        {
                            foreach (var app in targets) _deniedApps.Add(app);
                        }
                        _logger.LogInformation("Network application deny list updated. Count: {Count}", _deniedApps.Count);
                        break;

                    case "APP_ALLOW_LIST":
                        _allowedApps.Clear();
                        if (targets != null)
                        {
                            foreach (var app in targets) _allowedApps.Add(app);
                        }
                        _logger.LogInformation("Network application allow list updated. Count: {Count}", _allowedApps.Count);
                        break;

                    default:
                        _logger.LogWarning("Unknown network policy action: {Action}", action);
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply network policy: {Action}", action);
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task RollbackNetworkPoliciesAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Rolling back network policies...");

                foreach (var entry in _backupConfig)
                {
                    if (entry.Value == null)
                    {
                        _networkConfig.TryRemove(entry.Key, out _);
                    }
                    else
                    {
                        _networkConfig[entry.Key] = entry.Value;
                    }
                }

                _deniedApps.Clear();
                _allowedApps.Clear();
                _backupConfig.Clear();

                _logger.LogInformation("Network policies rolled back successfully.");
            }
            finally
            {
                _lock.Release();
            }
        }

        public string GetConfigValueForTest(string key)
        {
            _networkConfig.TryGetValue(key, out var val);
            return val;
        }

        public bool IsNetworkAccessAllowedForApp(string appName)
        {
            if (_deniedApps.Contains(appName)) return false;
            if (_allowedApps.Count > 0 && !_allowedApps.Contains(appName)) return false;
            return true;
        }
    }
}
