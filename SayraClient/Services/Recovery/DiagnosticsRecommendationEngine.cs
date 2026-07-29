using System;
using System.Collections.Generic;
using System.Linq;
using Sayra.Client.Shared.Models.Recovery;

namespace SayraClient.Services.Recovery
{
    /// <summary>
    /// Rule-based recommendation engine for formulating actionable system recommendations based on diagnostic telemetry.
    /// </summary>
    public static class DiagnosticsRecommendationEngine
    {
        /// <summary>
        /// Evaluates diagnostic datasets against rule definitions and outputs structured recommendations.
        /// </summary>
        public static List<string> EvaluateRules(
            IReadOnlyDictionary<string, SubsystemHealthInfo> healthSnapshot,
            RecoveryMetricsCollector? metricsCollector,
            ResourceMetrics? resourceMetrics,
            IReadOnlyList<SecurityValidationResult>? securityResults)
        {
            var recommendations = new List<string>();

            // Rule 1: High Memory Usage / Pressure
            if (resourceMetrics != null)
            {
                bool isHighRam = resourceMetrics.AvailableSystemRamBytes < (1024L * 1024 * 1024) || // < 1GB system RAM
                                 resourceMetrics.ProcessRamBytes > (800L * 1024 * 1024) || // > 800MB Process RAM
                                 resourceMetrics.PressureLevel == ResourcePressureLevel.Critical ||
                                 resourceMetrics.PressureLevel == ResourcePressureLevel.High;

                if (isHighRam)
                {
                    recommendations.Add("High memory usage detected. Action: Consider reducing active background processes, clearing local media caches, or executing an LRU eviction pass.");
                }

                if (resourceMetrics.FreeDiskSpaceBytes < (500L * 1024 * 1024)) // < 500MB
                {
                    recommendations.Add("Low local storage space detected. Action: Perform advertisement cache cleanups and purge temporary and completed update packages.");
                }

                if (resourceMetrics.CpuUsagePercentage > 90.0)
                {
                    recommendations.Add("High CPU resource consumption detected. Action: Check for CPU affinity leaks or zombie processes running in background gaming sessions.");
                }
            }

            // Rule 2: Repeated Subsystem Failures
            foreach (var kvp in healthSnapshot)
            {
                var sub = kvp.Value;
                // If health history shows repeated transitions or if last exception is logged
                int failureCount = sub.HealthHistory.Count(h => h.Contains("Critical") || h.Contains("Offline") || h.Contains("Failure") || h.Contains("Error"));
                if (sub.State == SubsystemHealthState.Critical || sub.State == SubsystemHealthState.Offline || failureCount >= 3)
                {
                    recommendations.Add($"Subsystem '{sub.SubsystemName}' has persistent failure or transition patterns (Current State: {sub.State}). Action: Inspect local SQLite logs, verify database connection constraints, and run manual self-healing tests.");
                }
            }

            // Rule 3: Recovery Loop Detected
            if (metricsCollector != null)
            {
                var histories = metricsCollector.GetAllHistory();
                foreach (var hist in histories)
                {
                    int totalAttempts = hist.RecoveryResults.Count;
                    bool hasLoop = totalAttempts >= 5 || hist.TotalFailures >= 5;
                    if (hasLoop)
                    {
                        recommendations.Add($"Recovery loop or storm detected for subsystem '{hist.SubsystemName}' (Attempts logged: {totalAttempts}). Action: Automatic self-healing is throttled to prevent resource exhaustion. Trigger operator intervention to inspect underlying dependency logs.");
                    }
                }
            }

            // Rule 4: Configuration Tampering
            if (securityResults != null)
            {
                var configCheck = securityResults.FirstOrDefault(r => r.TargetName.Equals("Configuration", StringComparison.OrdinalIgnoreCase) || r.TargetName.Equals("config", StringComparison.OrdinalIgnoreCase));
                if (configCheck != null && (configCheck.ValidationState == SecurityValidationState.Tampered || configCheck.ValidationState == SecurityValidationState.Failed))
                {
                    recommendations.Add("Configuration file tampering or invalid digital signature detected. Action: Re-fetch the cryptographically signed configuration package from the management dashboard and reload options.");
                }

                // Rule 5: Database Corruption
                var dbCheck = securityResults.FirstOrDefault(r => r.TargetName.Equals("Database", StringComparison.OrdinalIgnoreCase) || r.TargetName.Equals("database_reindex", StringComparison.OrdinalIgnoreCase));
                if (dbCheck != null && (dbCheck.ValidationState == SecurityValidationState.Tampered || dbCheck.ValidationState == SecurityValidationState.Failed))
                {
                    recommendations.Add("SQLCipher local database consistency check failed. Action: Execute SQLCipher PRAGMA integrity_check, reindex database, or trigger a complete localized database rollback/recreation.");
                }

                // Policy check
                var policyCheck = securityResults.FirstOrDefault(r => r.TargetName.Equals("Policy", StringComparison.OrdinalIgnoreCase));
                if (policyCheck != null && (policyCheck.ValidationState == SecurityValidationState.Tampered || policyCheck.ValidationState == SecurityValidationState.Failed))
                {
                    recommendations.Add("Local workstation security policies have failed signature validation. Action: Revoke current policies immediately, fall back to offline safety profiles, and initiate active sync with the server.");
                }
            }

            // Rule 6: Network Instability
            if (healthSnapshot.TryGetValue("Network", out var netInfo) && (netInfo.State == SubsystemHealthState.Offline || netInfo.State == SubsystemHealthState.Critical))
            {
                recommendations.Add("Network/Broker subsystem is Offline. Action: Validate local gateway connectivity, check physical ethernet cables, and verify SslStream / TLS 1.3 socket pinning certificates.");
            }

            // Add default stable suggestion if no critical issues found
            if (recommendations.Count == 0)
            {
                recommendations.Add("All monitored subsystem components, resources, and cryptographic trust boundaries are 100% operational. Action: None required. Continue routine operations.");
            }

            return recommendations;
        }
    }
}
