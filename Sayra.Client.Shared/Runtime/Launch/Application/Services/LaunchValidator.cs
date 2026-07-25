using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Launch.Domain.Models;
using Sayra.Client.Shared.Runtime.Launch.Domain.Exceptions;
using Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;
using Sayra.Client.Shared.Security.GameProtection.Domain.Models;
using Sayra.Client.Shared.Security.GameProtection.Domain.Rules;

namespace Sayra.Client.Shared.Runtime.Launch.Application.Services
{
    public class LaunchValidator : ILaunchValidator
    {
        private readonly ILogger<LaunchValidator> _logger;
        private readonly IIntegrityValidator? _integrityValidator;
        private readonly IProcessPolicyEvaluator? _policyEvaluator;

        public LaunchValidator(
            ILogger<LaunchValidator> logger,
            IIntegrityValidator? integrityValidator = null,
            IProcessPolicyEvaluator? policyEvaluator = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _integrityValidator = integrityValidator;
            _policyEvaluator = policyEvaluator;
        }

        public Task ValidateAsync(LaunchRequest request, LaunchProfile profile)
        {
            _logger.LogInformation("Performing launch validation for GameId: '{GameId}'", request.GameId);

            // 1. Check path validity
            if (string.IsNullOrWhiteSpace(request.ExecutablePath))
            {
                throw new LaunchValidationException("Executable path is null or empty.");
            }

            // 2. Check file existence
            if (!File.Exists(request.ExecutablePath))
            {
                throw new LaunchValidationException($"Game executable not found at specified path: '{request.ExecutablePath}'");
            }

            // 3. Validate executable extension
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string extension = Path.GetExtension(request.ExecutablePath);
                if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
                {
                    throw new LaunchValidationException($"Invalid executable extension: '{extension}'. Only Windows executables/scripts are allowed.");
                }
            }

            // 4. Integrate with Track 4.6 Integrity Verification
            if (_integrityValidator != null)
            {
                _logger.LogInformation("Running Track 4.6 Game Integrity checks for '{Path}'", request.ExecutablePath);
                var integrityResult = _integrityValidator.ValidateExecutable(request.ExecutablePath, expectedHash: string.Empty);
                if (integrityResult.Status == IntegrityStatus.Invalid)
                {
                    throw new LaunchValidationException($"Track 4.6 integrity validation failed. Reason: {integrityResult.Reason}");
                }
            }

            // 5. Integrate with Track 4.6 Process Policy Evaluation
            if (_policyEvaluator != null)
            {
                _logger.LogInformation("Running Track 4.6 Process Policy Evaluation for '{Path}'", request.ExecutablePath);
                var processInfo = new ProcessInfo
                {
                    ProcessId = 0,
                    ProcessName = Path.GetFileName(request.ExecutablePath),
                    ExecutablePath = request.ExecutablePath,
                    Hash = string.Empty,
                    Publisher = string.Empty
                };

                var decision = _policyEvaluator.Evaluate(processInfo);
                if (decision.Action == ProcessAction.Terminate || decision.Action == ProcessAction.Block)
                {
                    throw new LaunchValidationException($"Launch rejected by Track 4.6 security policy. Reason: {decision.Reason}");
                }
            }

            _logger.LogInformation("Launch validation succeeded for '{Path}'", request.ExecutablePath);
            return Task.CompletedTask;
        }
    }
}
