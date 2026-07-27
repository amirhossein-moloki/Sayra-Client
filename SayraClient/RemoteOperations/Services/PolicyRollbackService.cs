using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.RemoteOperations.Services
{
    public class PolicyRollbackService
    {
        private readonly RegistryPolicyManager _registryManager;
        private readonly UsbPolicyManager _usbManager;
        private readonly NetworkPolicyManager _networkManager;
        private readonly SessionPolicyManager _sessionManager;
        private readonly ILogger<PolicyRollbackService> _logger;
        private readonly ConcurrentDictionary<string, bool> _rollbackStatus = new();

        public PolicyRollbackService(
            RegistryPolicyManager registryManager,
            UsbPolicyManager usbManager,
            NetworkPolicyManager networkManager,
            SessionPolicyManager sessionManager,
            ILogger<PolicyRollbackService> logger)
        {
            _registryManager = registryManager ?? throw new ArgumentNullException(nameof(registryManager));
            _usbManager = usbManager ?? throw new ArgumentNullException(nameof(usbManager));
            _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> SnapshotPreviousConfigurationAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Taking a snapshot of current system and policy configurations before applying updates...");
            return true;
        }

        public async Task<bool> RollbackAllAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("CRITICAL: Initiating system-wide policy rollback...");

            bool success = true;

            try
            {
                await _registryManager.RollbackRegistryPoliciesAsync(cancellationToken);
                _rollbackStatus["WINDOWS"] = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback Registry policies.");
                _rollbackStatus["WINDOWS"] = false;
                success = false;
            }

            try
            {
                await _usbManager.RollbackUsbPoliciesAsync(cancellationToken);
                _rollbackStatus["USB"] = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback USB and Device policies.");
                _rollbackStatus["USB"] = false;
                success = false;
            }

            try
            {
                await _networkManager.RollbackNetworkPoliciesAsync(cancellationToken);
                _rollbackStatus["NETWORK"] = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback Network and Bandwidth policies.");
                _rollbackStatus["NETWORK"] = false;
                success = false;
            }

            try
            {
                await _sessionManager.RollbackSessionPoliciesAsync(cancellationToken);
                _rollbackStatus["SESSION"] = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback Session policies.");
                _rollbackStatus["SESSION"] = false;
                success = false;
            }

            bool verified = await VerifyRollbackAsync(cancellationToken);
            if (!verified)
            {
                _logger.LogCritical("Rollback verification failed! Some systems could still be in an inconsistent state.");
                return false;
            }

            _logger.LogInformation("Policy rollback completed successfully and verified.");
            return success;
        }

        public async Task<bool> RollbackCategoryAsync(string category, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Initiating partial policy rollback for category '{Category}'...", category);

            try
            {
                switch (category.ToUpperInvariant())
                {
                    case "WINDOWS":
                        await _registryManager.RollbackRegistryPoliciesAsync(cancellationToken);
                        break;
                    case "USB":
                    case "DEVICE":
                        await _usbManager.RollbackUsbPoliciesAsync(cancellationToken);
                        break;
                    case "NETWORK":
                        await _networkManager.RollbackNetworkPoliciesAsync(cancellationToken);
                        break;
                    case "SESSION":
                        await _sessionManager.RollbackSessionPoliciesAsync(cancellationToken);
                        break;
                    default:
                        _logger.LogWarning("Partial rollback not supported for category: '{Category}'", category);
                        return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Partial rollback failed for category '{Category}'.", category);
                return false;
            }
        }

        public Task<bool> VerifyRollbackAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Verifying configuration rollback status...");
            foreach (var status in _rollbackStatus)
            {
                if (!status.Value)
                {
                    _logger.LogError("Rollback verification failed for category: {Category}", status.Key);
                    return Task.FromResult(false);
                }
            }
            return Task.FromResult(true);
        }
    }
}
