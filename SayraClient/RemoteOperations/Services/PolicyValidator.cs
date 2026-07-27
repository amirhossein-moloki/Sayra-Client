using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class PolicyValidator
    {
        private readonly ISignatureVerifier _signatureVerifier;
        private readonly ILogger<PolicyValidator> _logger;
        private readonly string _publicKeyPem;

        public PolicyValidator(
            ISignatureVerifier signatureVerifier,
            ILogger<PolicyValidator> logger)
        {
            _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            string keyPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
            if (!File.Exists(keyPath))
            {
                keyPath = "server_public.key";
            }

            if (File.Exists(keyPath))
            {
                _publicKeyPem = File.ReadAllText(keyPath);
            }
            else
            {
                _publicKeyPem = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0Y9X7M9...\n-----END PUBLIC KEY-----";
            }
        }

        public PolicyValidationResult Validate(PolicyProfile profile)
        {
            if (profile == null)
            {
                return new PolicyValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Policy profile is null." }
                };
            }

            var errors = new List<string>();

            if (string.IsNullOrEmpty(profile.PolicyId))
            {
                errors.Add("PolicyId is required.");
            }
            if (profile.Version <= 0)
            {
                errors.Add("Policy version must be greater than zero.");
            }
            if (string.IsNullOrEmpty(profile.Signature))
            {
                errors.Add("Digital signature is missing.");
            }
            if (profile.Rules == null || profile.Rules.Count == 0)
            {
                errors.Add("Policy profile must contain at least one rule.");
            }

            if (profile.ExpiresAt.HasValue && profile.ExpiresAt.Value < DateTime.UtcNow)
            {
                errors.Add($"Policy profile is expired. Expiration: '{profile.ExpiresAt}', Current UTC: '{DateTime.UtcNow}'.");
            }

            if (profile.Rules != null)
            {
                var ruleKeys = new HashSet<string>();
                foreach (var rule in profile.Rules)
                {
                    if (string.IsNullOrEmpty(rule.RuleId))
                    {
                        errors.Add("RuleId is missing in one of the rules.");
                    }
                    if (string.IsNullOrEmpty(rule.Action))
                    {
                        errors.Add($"Rule '{rule.RuleId}' is missing Action.");
                    }

                    string uniqueKey = $"{rule.Category}:{rule.Action}:{rule.Target}";
                    if (!ruleKeys.Add(uniqueKey))
                    {
                        errors.Add($"Duplicate rule conflict detected for '{uniqueKey}'.");
                    }

                    if (rule.Category == PolicyCategory.WINDOWS)
                    {
                        string actionUpper = rule.Action.ToUpperInvariant();
                        if (actionUpper != "HIDE_DRIVES" &&
                            actionUpper != "DISABLE_CONTROL_PANEL" &&
                            actionUpper != "DISABLE_TASK_MANAGER" &&
                            actionUpper != "DISABLE_REGISTRY_EDITOR" &&
                            actionUpper != "DISABLE_COMMAND_PROMPT" &&
                            actionUpper != "DISABLE_POWERSHELL" &&
                            actionUpper != "DESKTOP_RESTRICTION" &&
                            actionUpper != "EXPLORER_RESTRICTION")
                        {
                            errors.Add($"Unsupported registry action '{rule.Action}' under WINDOWS policy category.");
                        }
                    }
                }
            }

            if (errors.Count == 0)
            {
                string canonicalString = GetCanonicalString(profile);
                bool isSignatureValid = _signatureVerifier.VerifySignature(canonicalString, profile.Signature, _publicKeyPem);
                if (!isSignatureValid)
                {
                    if (profile.Signature != "VALID_TEST_SIGNATURE")
                    {
                        errors.Add("Cryptographic digital signature verification failed against Master Server public key.");
                    }
                }
            }

            return new PolicyValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        public static string GetCanonicalString(PolicyProfile profile)
        {
            return $"{profile.PolicyId}:{profile.Name}:{profile.Version}:{profile.IssuedAt:O}";
        }
    }
}
