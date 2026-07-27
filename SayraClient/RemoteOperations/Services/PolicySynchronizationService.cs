using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class PolicySynchronizationService
    {
        private readonly IPolicyEngine _policyEngine;
        private readonly IPolicyRepository _repository;
        private readonly PolicyValidator _validator;
        private readonly IAuditService _auditService;
        private readonly ILogger<PolicySynchronizationService> _logger;

        public PolicySynchronizationService(
            IPolicyEngine policyEngine,
            IPolicyRepository repository,
            PolicyValidator validator,
            IAuditService auditService,
            ILogger<PolicySynchronizationService> logger)
        {
            _policyEngine = policyEngine ?? throw new ArgumentNullException(nameof(policyEngine));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PolicyChangeResult> SynchronizePolicyAsync(PolicyProfile profile, CancellationToken cancellationToken = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            string correlationId = Guid.NewGuid().ToString();
            _logger.LogInformation("Policy synchronization started. ProfileId: {PolicyId}, Version: {Version}, CorrelationId: {CorrelationId}",
                profile.PolicyId, profile.Version, correlationId);

            await _auditService.RecordPolicyEventAsync(profile.PolicyId, "POLICY_RECEIVED", $"Policy received. Version: {profile.Version}.", correlationId, cancellationToken);

            long currentVersion = await _repository.GetPolicyVersionAsync(cancellationToken);
            if (profile.Version < currentVersion)
            {
                string downgradeMsg = $"Downgrade rejection! Stored version code '{currentVersion}' is greater than incoming version '{profile.Version}'.";
                _logger.LogWarning(downgradeMsg);

                await _auditService.RecordPolicyEventAsync(profile.PolicyId, "POLICY_REJECTED", downgradeMsg, correlationId, cancellationToken);

                return new PolicyChangeResult
                {
                    Success = false,
                    Errors = new() { downgradeMsg }
                };
            }

            if (profile.Version == currentVersion)
            {
                string skipMsg = $"Incoming version code '{profile.Version}' is equal to stored active version. No sync needed.";
                _logger.LogInformation(skipMsg);
                return new PolicyChangeResult
                {
                    Success = true,
                    Errors = new() { skipMsg }
                };
            }

            var validationRes = _validator.Validate(profile);
            if (!validationRes.IsValid)
            {
                string validationErrors = string.Join("; ", validationRes.Errors);
                _logger.LogWarning("Incoming policy profile '{PolicyId}' failed validation: {Errors}", profile.PolicyId, validationErrors);

                await _auditService.RecordPolicyEventAsync(profile.PolicyId, "POLICY_REJECTED", $"Validation failed: {validationErrors}", correlationId, cancellationToken);

                return new PolicyChangeResult
                {
                    Success = false,
                    Errors = validationRes.Errors
                };
            }

            await _auditService.RecordPolicyEventAsync(profile.PolicyId, "POLICY_VALIDATED", "Policy validation completed successfully.", correlationId, cancellationToken);

            var changeResult = await _policyEngine.ApplyPoliciesAsync(profile, cancellationToken);

            if (changeResult.Success)
            {
                _logger.LogInformation("Successfully synchronized and applied policy version '{Version}'", profile.Version);

                await _auditService.RecordPolicyEventAsync(profile.PolicyId, "POLICY_APPLIED", $"Policy applied successfully. Version: {profile.Version}.", correlationId, cancellationToken);
            }
            else
            {
                string applyErrors = string.Join("; ", changeResult.Errors);
                _logger.LogError("Failed to apply policy profile. Rollback was performed automatically. Errors: {Errors}", applyErrors);

                await _auditService.RecordPolicyEventAsync(profile.PolicyId, "POLICY_ROLLBACK", $"Automatic rollback due to application failure: {applyErrors}", correlationId, cancellationToken);

                await _auditService.RecordPolicyEventAsync(profile.PolicyId, "POLICY_REJECTED", $"Policy application failed: {applyErrors}", correlationId, cancellationToken);
            }

            return changeResult;
        }

        public async Task<PolicyChangeResult> RemovePolicyAsync(string policyId, CancellationToken cancellationToken = default)
        {
            string correlationId = Guid.NewGuid().ToString();
            _logger.LogInformation("Request received to remove policy '{PolicyId}'.", policyId);

            var res = await _policyEngine.RemovePoliciesAsync(policyId, cancellationToken);

            if (res.Success)
            {
                await _auditService.RecordPolicyEventAsync(policyId, "POLICY_REMOVED", $"Policy profile '{policyId}' has been successfully removed.", correlationId, cancellationToken);
            }

            return res;
        }
    }
}
