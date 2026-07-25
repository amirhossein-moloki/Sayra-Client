using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;
using Sayra.Client.Shared.Security.GameProtection.Domain.Models;
using Sayra.Client.Shared.Security.GameProtection.Domain.Rules;

namespace Sayra.Client.Shared.Security.GameProtection.Application.Services;

public class ProcessPolicyEvaluator : IProcessPolicyEvaluator
{
    private readonly ProcessPolicy _policy;
    private readonly IIntegrityValidator _integrityValidator;

    public ProcessPolicyEvaluator(ProcessPolicy policy, IIntegrityValidator integrityValidator)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _integrityValidator = integrityValidator ?? throw new ArgumentNullException(nameof(integrityValidator));
    }

    public SecurityDecision Evaluate(ProcessInfo process)
    {
        if (process == null) throw new ArgumentNullException(nameof(process));

        var lowerName = process.ProcessName.ToLowerInvariant();
        var lowerPath = process.ExecutablePath.ToLowerInvariant();

        // 1. Evaluate explicit Blocked Applications (Blacklist)
        foreach (var blockedApp in _policy.BlockedApplications)
        {
            bool match = false;
            if (!string.IsNullOrEmpty(blockedApp.Name) && lowerName.Equals(blockedApp.Name.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            {
                match = true;
            }
            else if (!string.IsNullOrEmpty(blockedApp.Pattern) && (lowerName.Contains(blockedApp.Pattern.ToLowerInvariant()) || lowerPath.Contains(blockedApp.Pattern.ToLowerInvariant())))
            {
                match = true;
            }

            if (match)
            {
                return new SecurityDecision
                {
                    Action = ProcessAction.Terminate,
                    Reason = $"Blocked application detected matching pattern: {blockedApp.Name ?? blockedApp.Pattern}. Reason: {blockedApp.Reason}",
                    Severity = blockedApp.Severity,
                    RuleTriggered = new ProcessRule
                    {
                        ProcessName = blockedApp.Name,
                        PathPattern = blockedApp.Pattern,
                        Action = ProcessAction.Terminate,
                        Severity = blockedApp.Severity
                    }
                };
            }
        }

        // 2. Evaluate explicit custom ProcessRules
        // Evaluate Block/Terminate rules first (Priority)
        var negativeRules = _policy.Rules.Where(r => r.Action == ProcessAction.Block || r.Action == ProcessAction.Terminate);
        foreach (var rule in negativeRules)
        {
            if (IsRuleMatch(process, rule))
            {
                return new SecurityDecision
                {
                    Action = rule.Action,
                    Reason = $"Process matches custom restriction rule: '{rule.ProcessName}'",
                    Severity = rule.Severity,
                    RuleTriggered = rule
                };
            }
        }

        // 3. Evaluate explicit Allowed Games (Whitelist)
        var allowedGame = _policy.AllowedGames.FirstOrDefault(g =>
            g.IsEnabled &&
            (lowerName.Equals(g.ExecutableName.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) ||
             lowerPath.Equals(g.ExecutablePath.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)));

        if (allowedGame != null)
        {
            // Perform integrity checks
            if (!string.IsNullOrEmpty(allowedGame.ExpectedHash) || !string.IsNullOrEmpty(allowedGame.Publisher))
            {
                var integrity = _integrityValidator.ValidateExecutable(process.ExecutablePath, allowedGame.ExpectedHash, allowedGame.Publisher);
                if (integrity.Status == IntegrityStatus.Invalid)
                {
                    return new SecurityDecision
                    {
                        Action = ProcessAction.Terminate,
                        Reason = $"Whitelisted game failed integrity verification: {integrity.Reason}",
                        Severity = "Critical",
                        RuleTriggered = new ProcessRule
                        {
                            ProcessName = allowedGame.ExecutableName,
                            PathPattern = allowedGame.ExecutablePath,
                            Action = ProcessAction.Terminate,
                            Severity = "Critical"
                        }
                    };
                }
            }

            return new SecurityDecision
            {
                Action = ProcessAction.Allow,
                Reason = "Process matches whitelisted allowed game.",
                Severity = "None"
            };
        }

        // 4. Evaluate positive custom ProcessRules (Allow/Report)
        var positiveRules = _policy.Rules.Where(r => r.Action == ProcessAction.Allow || r.Action == ProcessAction.Report);
        foreach (var rule in positiveRules)
        {
            if (IsRuleMatch(process, rule))
            {
                return new SecurityDecision
                {
                    Action = rule.Action,
                    Reason = $"Process matches custom rule: '{rule.ProcessName}'",
                    Severity = rule.Severity,
                    RuleTriggered = rule
                };
            }
        }

        // 5. Strict Whitelisting Rule
        if (_policy.StrictWhitelistingEnabled)
        {
            return new SecurityDecision
            {
                Action = ProcessAction.Terminate,
                Reason = "Strict whitelisting is enabled and process is not whitelisted.",
                Severity = "High"
            };
        }

        // 6. Default fallback decision
        return new SecurityDecision
        {
            Action = ProcessAction.Allow,
            Reason = "No matching rules found, default to Allow.",
            Severity = "None"
        };
    }

    private bool IsRuleMatch(ProcessInfo process, ProcessRule rule)
    {
        var lowerName = process.ProcessName.ToLowerInvariant();
        var lowerPath = process.ExecutablePath.ToLowerInvariant();

        if (!string.IsNullOrEmpty(rule.ProcessName) && lowerName.Equals(rule.ProcessName.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(rule.PathPattern) && lowerPath.Contains(rule.PathPattern.ToLowerInvariant()))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(rule.Hash) && !string.IsNullOrEmpty(process.Hash) && process.Hash.Equals(rule.Hash, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
