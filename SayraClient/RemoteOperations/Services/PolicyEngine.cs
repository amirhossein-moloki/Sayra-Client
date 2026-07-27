using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class PolicyEngine : IPolicyEngine
    {
        private readonly IPolicyRepository _repository;
        private readonly PolicyValidator _validator;
        private readonly PolicyRollbackService _rollbackService;
        private readonly RegistryPolicyManager _registryManager;
        private readonly UsbPolicyManager _usbManager;
        private readonly NetworkPolicyManager _networkManager;
        private readonly SessionPolicyManager _sessionManager;
        private readonly ILogger<PolicyEngine> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public PolicyEngine(
            IPolicyRepository repository,
            PolicyValidator validator,
            PolicyRollbackService rollbackService,
            RegistryPolicyManager registryManager,
            UsbPolicyManager usbManager,
            NetworkPolicyManager networkManager,
            SessionPolicyManager sessionManager,
            ILogger<PolicyEngine> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _rollbackService = rollbackService ?? throw new ArgumentNullException(nameof(rollbackService));
            _registryManager = registryManager ?? throw new ArgumentNullException(nameof(registryManager));
            _usbManager = usbManager ?? throw new ArgumentNullException(nameof(usbManager));
            _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PolicyChangeResult> ApplyPoliciesAsync(PolicyProfile profile, CancellationToken cancellationToken = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Applying policy profile '{PolicyId}'...", profile.PolicyId);

                var validationResult = await ValidatePoliciesAsync(profile, cancellationToken);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Validation failed for policy profile '{PolicyId}'. Aborting apply.", profile.PolicyId);
                    return new PolicyChangeResult
                    {
                        Success = false,
                        Errors = validationResult.Errors
                    };
                }

                await _rollbackService.SnapshotPreviousConfigurationAsync(cancellationToken);

                var appliedRules = new List<string>();
                bool anyFailed = false;
                string failError = "";

                foreach (var rule in profile.Rules)
                {
                    try
                    {
                        bool success = false;
                        switch (rule.Category)
                        {
                            case PolicyCategory.WINDOWS:
                            case PolicyCategory.USER:
                            case PolicyCategory.SECURITY:
                                success = await _registryManager.ApplyRegistryPolicyAsync(rule.Action, rule.Value, cancellationToken);
                                break;

                            case PolicyCategory.USB:
                            case PolicyCategory.DEVICE:
                                List<string> usbDevices = null;
                                if (rule.Parameters != null && rule.Parameters.TryGetValue("Devices", out var devStr))
                                {
                                    usbDevices = new List<string>(devStr.Split(',', StringSplitOptions.RemoveEmptyEntries));
                                }
                                success = await _usbManager.ApplyUsbPolicyAsync(rule.Action, rule.Value, usbDevices, cancellationToken);
                                break;

                            case PolicyCategory.NETWORK:
                                List<string> netTargets = null;
                                if (rule.Parameters != null && rule.Parameters.TryGetValue("Targets", out var tarStr))
                                {
                                    netTargets = new List<string>(tarStr.Split(',', StringSplitOptions.RemoveEmptyEntries));
                                }
                                success = await _networkManager.ApplyNetworkPolicyAsync(rule.Action, rule.Value, netTargets, cancellationToken);
                                break;

                            case PolicyCategory.SESSION:
                                success = await _sessionManager.ApplySessionPolicyAsync(rule.Action, rule.Value, cancellationToken);
                                break;

                            default:
                                _logger.LogWarning("Skipping unsupported category rule action '{Action}'", rule.Action);
                                break;
                        }

                        if (success)
                        {
                            appliedRules.Add(rule.RuleId);
                        }
                        else
                        {
                            anyFailed = true;
                            failError = $"Rule '{rule.RuleId}' application returned false.";
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        anyFailed = true;
                        failError = $"Rule '{rule.RuleId}' application threw exception: {ex.Message}";
                        _logger.LogError(ex, "Failed applying rule '{RuleId}' in category '{Category}'", rule.RuleId, rule.Category);
                        break;
                    }
                }

                if (anyFailed)
                {
                    _logger.LogError("Policy application failed due to: {Error}. Triggering automatic rollback.", failError);
                    await _rollbackService.RollbackAllAsync(cancellationToken);
                    return new PolicyChangeResult
                    {
                        Success = false,
                        Errors = new List<string> { failError }
                    };
                }

                await _repository.SavePolicyAsync(profile, cancellationToken);

                _logger.LogInformation("Policy profile '{PolicyId}' successfully applied.", profile.PolicyId);
                return new PolicyChangeResult
                {
                    Success = true,
                    ModifiedRules = appliedRules
                };
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<PolicyChangeResult> RemovePoliciesAsync(string policyId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(policyId)) throw new ArgumentNullException(nameof(policyId));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Removing policy profile '{PolicyId}'...", policyId);

                await _rollbackService.RollbackAllAsync(cancellationToken);
                await _repository.DeletePolicyAsync(policyId, cancellationToken);

                return new PolicyChangeResult
                {
                    Success = true,
                    ModifiedRules = new List<string> { $"Removed policy: {policyId}" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove policy '{PolicyId}'.", policyId);
                return new PolicyChangeResult
                {
                    Success = false,
                    Errors = new List<string> { ex.Message }
                };
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<PolicyChangeResult> UpdatePoliciesAsync(PolicyProfile profile, CancellationToken cancellationToken = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            _logger.LogInformation("Updating policy profile '{PolicyId}' to version {Version}...", profile.PolicyId, profile.Version);
            return await ApplyPoliciesAsync(profile, cancellationToken);
        }

        public Task<PolicyValidationResult> ValidatePoliciesAsync(PolicyProfile profile, CancellationToken cancellationToken = default)
        {
            var res = _validator.Validate(profile);
            return Task.FromResult(res);
        }

        public async Task RollbackFailedPoliciesAsync(string policyId, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Explicit rollback triggered for policy profile '{PolicyId}'", policyId);
            await _rollbackService.RollbackAllAsync(cancellationToken);
        }
    }
}
