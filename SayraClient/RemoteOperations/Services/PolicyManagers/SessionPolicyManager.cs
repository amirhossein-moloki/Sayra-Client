using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SayraClient.Kiosk.Application.Interfaces;

namespace SayraClient.RemoteOperations.Services
{
    public class SessionPolicyManager
    {
        private readonly ILogger<SessionPolicyManager> _logger;
        private readonly IMaintenanceModeService? _maintenanceModeService;
        private readonly ConcurrentDictionary<string, object> _sessionSettings = new();
        private readonly ConcurrentDictionary<string, object> _backupSettings = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        public SessionPolicyManager(
            ILogger<SessionPolicyManager> logger,
            IMaintenanceModeService? maintenanceModeService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maintenanceModeService = maintenanceModeService;
        }

        public async Task<bool> ApplySessionPolicyAsync(string action, string value, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Applying session policy action '{Action}' with value '{Value}'", action, value);

                _backupSettings.TryAdd(action, _sessionSettings.TryGetValue(action, out var oldVal) ? oldVal : null);

                switch (action.ToUpperInvariant())
                {
                    case "AUTO_LOGOUT":
                        _sessionSettings["AutoLogout"] = value == "true" || value == "1";
                        _logger.LogInformation("Session auto logout configured to: {Value}", value);
                        break;

                    case "SESSION_TIMEOUT":
                        if (int.TryParse(value, out int minutes))
                        {
                            _sessionSettings["SessionTimeoutMinutes"] = minutes;
                            _logger.LogInformation("Session timeout limit set to {Value} minutes.", minutes);
                        }
                        break;

                    case "MAINTENANCE_MODE":
                        if (value == "true" || value == "1")
                        {
                            if (_maintenanceModeService != null)
                            {
                                await _maintenanceModeService.EnterMaintenanceModeAsync("DefaultAdminPassword123!");
                            }
                            _sessionSettings["MaintenanceModeActive"] = true;
                            _logger.LogInformation("System maintenance mode activated.");
                        }
                        else
                        {
                            _maintenanceModeService?.ExitMaintenanceMode();
                            _sessionSettings["MaintenanceModeActive"] = false;
                            _logger.LogInformation("System maintenance mode deactivated.");
                        }
                        break;

                    case "IDLE_TIMEOUT":
                        if (int.TryParse(value, out int idleMins))
                        {
                            _sessionSettings["IdleTimeoutMinutes"] = idleMins;
                            _logger.LogInformation("Session idle timeout set to {Value} minutes.", idleMins);
                        }
                        break;

                    case "SESSION_LOCK":
                        _sessionSettings["SessionLockActive"] = value == "true" || value == "1";
                        _logger.LogInformation("Session lock policy configured to: {Value}", value);
                        break;

                    case "KIOSK_ENFORCEMENT":
                        _sessionSettings["KioskEnforcementActive"] = value == "true" || value == "1";
                        _logger.LogInformation("Kiosk mode enforcement status set to: {Value}", value);
                        break;

                    default:
                        _logger.LogWarning("Unknown session policy action: {Action}", action);
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply session policy: {Action}", action);
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task RollbackSessionPoliciesAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Rolling back session policies...");

                foreach (var entry in _backupSettings)
                {
                    if (entry.Value == null)
                    {
                        _sessionSettings.TryRemove(entry.Key, out _);
                        if (entry.Key.Equals("MAINTENANCE_MODE", StringComparison.OrdinalIgnoreCase))
                        {
                            _maintenanceModeService?.ExitMaintenanceMode();
                        }
                    }
                    else
                    {
                        _sessionSettings[entry.Key] = entry.Value;
                        if (entry.Key.Equals("MAINTENANCE_MODE", StringComparison.OrdinalIgnoreCase))
                        {
                            bool active = (bool)entry.Value;
                            if (!active) _maintenanceModeService?.ExitMaintenanceMode();
                        }
                    }
                }

                _backupSettings.Clear();
                _logger.LogInformation("Session policies rolled back successfully.");
            }
            finally
            {
                _lock.Release();
            }
        }

        public object GetSettingForTest(string key)
        {
            _sessionSettings.TryGetValue(key, out var val);
            return val;
        }
    }
}
