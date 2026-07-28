using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Validation;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Engine for evaluating workstation update eligibility against multiple system and policy rules.
    /// </summary>
    public class EligibilityEvaluator : IEligibilityEvaluator
    {
        private readonly IMaintenanceWindowService _maintenanceWindowService;
        private readonly IDeploymentPolicyEvaluator _policyEvaluator;
        private readonly IVersionValidator _versionValidator;
        private readonly DeploymentOptions _deploymentOptions;

        public EligibilityEvaluator(
            IMaintenanceWindowService maintenanceWindowService,
            IDeploymentPolicyEvaluator policyEvaluator,
            IVersionValidator versionValidator,
            IOptions<DeploymentOptions> deploymentOptions)
        {
            _maintenanceWindowService = maintenanceWindowService ?? throw new ArgumentNullException(nameof(maintenanceWindowService));
            _policyEvaluator = policyEvaluator ?? throw new ArgumentNullException(nameof(policyEvaluator));
            _versionValidator = versionValidator ?? throw new ArgumentNullException(nameof(versionValidator));
            _deploymentOptions = deploymentOptions?.Value ?? new DeploymentOptions();
        }

        public async Task<EligibilityResult> EvaluateEligibilityAsync(
            UpdateManifest manifest,
            bool hasActiveSession,
            bool hasPendingOperations,
            CancellationToken cancellationToken)
        {
            if (manifest == null)
            {
                return new EligibilityResult { IsEligible = false, Reasons = { "Manifest is null." } };
            }

            var result = new EligibilityResult { IsEligible = true };

            try
            {
                _versionValidator.Validate(_deploymentOptions.CurrentVersion);
                _versionValidator.Validate(manifest.Version);

                if (!string.IsNullOrWhiteSpace(manifest.MinimumClientVersion))
                {
                    _versionValidator.Validate(manifest.MinimumClientVersion);
                }
            }
            catch (Exception ex)
            {
                result.IsEligible = false;
                result.Reasons.Add($"Version validation failed: {ex.Message}");
                return result;
            }

            // 1. Current Version vs Target Version
            int targetComparison = CompareVersions(_deploymentOptions.CurrentVersion, manifest.Version);
            if (targetComparison == 0)
            {
                result.IsEligible = false;
                result.Reasons.Add("Workstation is already on the target version.");
            }
            else if (targetComparison > 0)
            {
                // Downgrade rules: blocked unless forced
                if (manifest.Priority != UpdatePriority.Critical)
                {
                    result.IsEligible = false;
                    result.Reasons.Add($"Downgrades from {_deploymentOptions.CurrentVersion} to {manifest.Version} are blocked.");
                }
            }

            // 2. Minimum Supported Version Check
            bool belowMinRequired = false;
            if (!string.IsNullOrWhiteSpace(manifest.MinimumClientVersion))
            {
                if (CompareVersions(_deploymentOptions.CurrentVersion, manifest.MinimumClientVersion) < 0)
                {
                    belowMinRequired = true;
                    result.Reasons.Add($"Current version {_deploymentOptions.CurrentVersion} is below the minimum required version {manifest.MinimumClientVersion}. Funneled to forced immediate upgrade.");
                }
            }

            // 3. Deployment Ring Check
            if (_deploymentOptions.Ring == DeploymentRing.Production && manifest.Channel == UpdateChannel.Beta)
            {
                result.IsEligible = false;
                result.Reasons.Add("Beta updates are restricted for Production deployment ring.");
            }

            // 4. Update Policy
            var policy = new DeploymentPolicy
            {
                IsAutomatic = _deploymentOptions.AutoUpdate,
                IsForced = manifest.IsForcedUpgrade || belowMinRequired || _deploymentOptions.ForceImmediate,
                IsEmergency = manifest.Priority == UpdatePriority.Critical,
                IsOptional = manifest.Priority == UpdatePriority.Low
            };

            try
            {
                bool policyPermitted = _policyEvaluator.EvaluatePolicy(manifest, policy);
                if (!policyPermitted)
                {
                    result.IsEligible = false;
                    result.Reasons.Add("Deployment policy restricts automatic update installation.");
                }
            }
            catch (Exception ex)
            {
                result.IsEligible = false;
                result.Reasons.Add($"Policy evaluation exception: {ex.Message}");
            }

            // 5. Maintenance Window Check
            bool isForced = _policyEvaluator.IsForcedUpdate(manifest, policy);
            bool isInsideWindow = _maintenanceWindowService.IsInsideWindow(DateTime.UtcNow);
            if (!isInsideWindow && !isForced)
            {
                result.IsEligible = false;
                result.Reasons.Add("Outside configured maintenance window and the update is not forced/emergency.");
            }

            // 6. Active Sessions Check
            if (hasActiveSession && !isForced && !_deploymentOptions.BypassActiveUserSession)
            {
                result.IsEligible = false;
                result.Reasons.Add("An active user/game session is currently in progress.");
            }

            // 7. Pending Operations Check
            if (hasPendingOperations && !isForced)
            {
                result.IsEligible = false;
                result.Reasons.Add("Other system operations are currently pending.");
            }

            // Filter out non-blocking reasons
            if (result.Reasons.Any(r => !r.Contains("Funneled to forced immediate upgrade")))
            {
                result.IsEligible = false;
            }

            // If below absolute minimum supported version, we must force eligibility
            if (belowMinRequired)
            {
                result.IsEligible = true;
            }

            return result;
        }

        private int CompareVersions(string version1, string version2)
        {
            var mainPart1 = version1.Split('+')[0].Split('-')[0];
            var mainPart2 = version2.Split('+')[0].Split('-')[0];

            var parts1 = mainPart1.Split('.').Select(int.Parse).ToArray();
            var parts2 = mainPart2.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Min(parts1.Length, parts2.Length); i++)
            {
                if (parts1[i] > parts2[i]) return 1;
                if (parts1[i] < parts2[i]) return -1;
            }

            if (parts1.Length > parts2.Length) return 1;
            if (parts1.Length < parts2.Length) return -1;

            bool isPre1 = version1.Contains("-");
            bool isPre2 = version2.Contains("-");

            if (isPre1 && !isPre2) return -1;
            if (!isPre1 && isPre2) return 1;

            if (isPre1 && isPre2)
            {
                string pre1 = version1.Split('-')[1].Split('+')[0];
                string pre2 = version2.Split('-')[1].Split('+')[0];
                return string.Compare(pre1, pre2, StringComparison.OrdinalIgnoreCase);
            }

            return 0;
        }
    }
}
