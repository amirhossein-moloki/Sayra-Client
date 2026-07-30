using System;
using System.IO;
using System.Text.RegularExpressions;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;

namespace SayraClient.Services.Recovery
{
    /// <summary>
    /// Production-grade implementation of the Resilience Configuration Validator.
    /// Performs exhaustive required-value, range, schema, and cross-option validation on the resilience configuration profile.
    /// </summary>
    public class ResilienceConfigurationValidator : IConfigurationValidator
    {
        private static readonly Regex VersionRegex = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);

        /// <inheritdoc />
        public ConfigurationValidationResult Validate(ResilienceConfiguration configuration)
        {
            var result = new ConfigurationValidationResult();

            if (configuration == null)
            {
                result.AddError("Configuration cannot be null.");
                return result;
            }

            // 1. Version and Schema Validation
            ValidateVersionAndSchema(configuration, result);

            // 2. Health Monitor Options Validation
            ValidateHealthMonitor(configuration.HealthMonitor, result);

            // 3. Self-Healing Options Validation
            ValidateSelfHealing(configuration.SelfHealing, result);

            // 4. Crash Recovery Options Validation
            ValidateCrashRecovery(configuration.CrashRecovery, result);

            // 5. Resource Monitor Options Validation
            ValidateResourceMonitor(configuration.ResourceMonitor, result);

            // 6. Security Hardening Options Validation
            ValidateSecurityHardening(configuration.SecurityHardening, result);

            // 7. Graceful Shutdown Options Validation
            ValidateGracefulShutdown(configuration.GracefulShutdown, result);

            // 8. Recovery Diagnostics Options Validation
            ValidateDiagnostics(configuration.Diagnostics, result);

            // 9. Watchdog Options Validation
            ValidateWatchdog(configuration.Watchdog, result);

            return result;
        }

        private void ValidateVersionAndSchema(ResilienceConfiguration configuration, ConfigurationValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(configuration.SchemaVersion))
            {
                result.AddError("Configuration 'SchemaVersion' is required.");
                return;
            }

            if (!VersionRegex.IsMatch(configuration.SchemaVersion))
            {
                result.AddError($"Configuration version '{configuration.SchemaVersion}' does not match expected semver schema format (e.g. 1.0.0).");
                return;
            }

            var majorVersion = configuration.SchemaVersion.Split('.')[0];
            if (majorVersion != "1")
            {
                result.AddError($"Incompatible configuration version '{configuration.SchemaVersion}'. Major version '1' is required.");
            }
        }

        private void ValidateHealthMonitor(HealthMonitorOptions options, ConfigurationValidationResult result)
        {
            if (options == null)
            {
                result.AddError("HealthMonitorOptions block cannot be null.");
                return;
            }

            if (options.DefaultHeartbeatTimeout <= TimeSpan.Zero)
            {
                result.AddError("DefaultHeartbeatTimeout must be a positive, non-zero duration.");
            }

            if (options.BaseFailureDeduction < 0)
            {
                result.AddError("BaseFailureDeduction cannot be negative.");
            }

            if (options.BaseTransitionDeduction < 0)
            {
                result.AddError("BaseTransitionDeduction cannot be negative.");
            }

            if (options.DependencyFailureDeduction < 0)
            {
                result.AddError("DependencyFailureDeduction cannot be negative.");
            }

            if (options.MaxHistoricalSnapshots <= 0)
            {
                result.AddError("MaxHistoricalSnapshots must be greater than zero.");
            }
        }

        private void ValidateSelfHealing(SelfHealingOptions options, ConfigurationValidationResult result)
        {
            if (options == null)
            {
                result.AddError("SelfHealingOptions block cannot be null.");
                return;
            }

            if (options.MaxAttempts <= 0)
            {
                result.AddError("MaxAttempts must be a positive integer greater than zero.");
            }

            if (options.AttemptsResetDuration <= TimeSpan.Zero)
            {
                result.AddError("AttemptsResetDuration must be a positive, non-zero duration.");
            }

            if (options.SubsystemPolicies == null)
            {
                result.AddError("SubsystemPolicies list cannot be null.");
            }
            else
            {
                foreach (var policy in options.SubsystemPolicies)
                {
                    if (string.IsNullOrWhiteSpace(policy.SubsystemName))
                    {
                        result.AddError("A defined recovery policy has a missing or empty SubsystemName.");
                    }

                    if (policy.Retry == null)
                    {
                        result.AddError($"Policy for '{policy.SubsystemName}' has a null Retry block.");
                    }
                    else
                    {
                        if (policy.Retry.MaxRetries < 0)
                        {
                            result.AddError($"Policy '{policy.SubsystemName}' Retry.MaxRetries cannot be negative.");
                        }

                        if (policy.Retry.InitialDelay < TimeSpan.Zero)
                        {
                            result.AddError($"Policy '{policy.SubsystemName}' Retry.InitialDelay cannot be negative.");
                        }
                    }
                }
            }
        }

        private void ValidateCrashRecovery(CrashRecoveryOptions options, ConfigurationValidationResult result)
        {
            if (options == null)
            {
                result.AddError("CrashRecoveryOptions block cannot be null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(options.ShutdownStateFilePath))
            {
                result.AddError("ShutdownStateFilePath is required.");
            }
        }

        private void ValidateResourceMonitor(ResourceMonitorOptions options, ConfigurationValidationResult result)
        {
            if (options == null)
            {
                result.AddError("ResourceMonitorOptions block cannot be null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(options.MachineIdentifier))
            {
                result.AddError("MachineIdentifier is required.");
            }

            if (options.SamplingInterval <= TimeSpan.Zero)
            {
                result.AddError("SamplingInterval must be a positive duration.");
            }

            // CPU validation
            if (options.CpuWarningThreshold < 0 || options.CpuWarningThreshold > 100 ||
                options.CpuCriticalThreshold < 0 || options.CpuCriticalThreshold > 100 ||
                options.CpuEmergencyThreshold < 0 || options.CpuEmergencyThreshold > 100)
            {
                result.AddError("CPU usage thresholds must be between 0.0 and 100.0 percent.");
            }

            if (options.CpuWarningThreshold >= options.CpuCriticalThreshold)
            {
                result.AddError("CpuWarningThreshold must be strictly less than CpuCriticalThreshold.");
            }

            if (options.CpuCriticalThreshold >= options.CpuEmergencyThreshold)
            {
                result.AddError("CpuCriticalThreshold must be strictly less than CpuEmergencyThreshold.");
            }

            // GPU validation
            if (options.GpuWarningThreshold < 0 || options.GpuWarningThreshold > 100 ||
                options.GpuCriticalThreshold < 0 || options.GpuCriticalThreshold > 100 ||
                options.GpuEmergencyThreshold < 0 || options.GpuEmergencyThreshold > 100)
            {
                result.AddError("GPU usage thresholds must be between 0.0 and 100.0 percent.");
            }

            if (options.GpuWarningThreshold >= options.GpuCriticalThreshold)
            {
                result.AddError("GpuWarningThreshold must be strictly less than GpuCriticalThreshold.");
            }

            if (options.GpuCriticalThreshold >= options.GpuEmergencyThreshold)
            {
                result.AddError("GpuCriticalThreshold must be strictly less than GpuEmergencyThreshold.");
            }

            // RAM validation
            if (options.ProcessRamWarningBytes <= 0 || options.ProcessRamCriticalBytes <= 0 || options.ProcessRamEmergencyBytes <= 0)
            {
                result.AddError("Process RAM thresholds must be positive values.");
            }

            if (options.ProcessRamWarningBytes >= options.ProcessRamCriticalBytes)
            {
                result.AddError("ProcessRamWarningBytes must be strictly less than ProcessRamCriticalBytes.");
            }

            if (options.ProcessRamCriticalBytes >= options.ProcessRamEmergencyBytes)
            {
                result.AddError("ProcessRamCriticalBytes must be strictly less than ProcessRamEmergencyBytes.");
            }

            // System available RAM validation
            if (options.SystemAvailableRamWarningBytes <= 0 || options.SystemAvailableRamCriticalBytes <= 0 || options.SystemAvailableRamEmergencyBytes <= 0)
            {
                result.AddError("System available RAM thresholds must be positive values.");
            }

            // System available RAM represents free memory - Warning threshold is higher than critical!
            if (options.SystemAvailableRamWarningBytes <= options.SystemAvailableRamCriticalBytes)
            {
                result.AddError("SystemAvailableRamWarningBytes must be strictly greater than SystemAvailableRamCriticalBytes (since it represents free RAM threshold).");
            }

            if (options.SystemAvailableRamCriticalBytes <= options.SystemAvailableRamEmergencyBytes)
            {
                result.AddError("SystemAvailableRamCriticalBytes must be strictly greater than SystemAvailableRamEmergencyBytes (since it represents free RAM threshold).");
            }

            // Handle thresholds cross validation
            if (options.HandleWarningThreshold >= options.HandleCriticalThreshold ||
                options.HandleCriticalThreshold >= options.HandleEmergencyThreshold)
            {
                result.AddError("Handle thresholds must satisfy: Warning < Critical < Emergency.");
            }

            // Thread thresholds cross validation
            if (options.ThreadWarningThreshold >= options.ThreadCriticalThreshold ||
                options.ThreadCriticalThreshold >= options.ThreadEmergencyThreshold)
            {
                result.AddError("Thread thresholds must satisfy: Warning < Critical < Emergency.");
            }

            // GDI thresholds cross validation
            if (options.GdiWarningThreshold >= options.GdiCriticalThreshold ||
                options.GdiCriticalThreshold >= options.GdiEmergencyThreshold)
            {
                result.AddError("GDI thresholds must satisfy: Warning < Critical < Emergency.");
            }
        }

        private void ValidateSecurityHardening(SecurityHardeningOptions options, ConfigurationValidationResult result)
        {
            if (options == null)
            {
                result.AddError("SecurityHardeningOptions block cannot be null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(options.PublicKeyPath))
            {
                result.AddError("PublicKeyPath is required.");
            }

            if (options.GlobalPolicy == null)
            {
                result.AddError("Global security policy block cannot be null.");
            }
        }

        private void ValidateGracefulShutdown(GracefulShutdownOptions options, ConfigurationValidationResult result)
        {
            if (options == null)
            {
                result.AddError("GracefulShutdownOptions block cannot be null.");
                return;
            }

            if (options.StopWorkTimeout <= TimeSpan.Zero ||
                options.StopDownloadsTimeout <= TimeSpan.Zero ||
                options.DrainQueuesTimeout <= TimeSpan.Zero ||
                options.FlushLogsTimeout <= TimeSpan.Zero ||
                options.PersistStatesTimeout <= TimeSpan.Zero ||
                options.StopWorkersTimeout <= TimeSpan.Zero ||
                options.CloseDatabaseTimeout <= TimeSpan.Zero)
            {
                result.AddError("All individual graceful shutdown timeouts must be positive durations.");
            }

            if (options.OverallTimeout <= TimeSpan.Zero)
            {
                result.AddError("Graceful shutdown OverallTimeout must be a positive duration.");
            }
        }

        private void ValidateDiagnostics(RecoveryDiagnosticsOptions options, ConfigurationValidationResult result)
        {
            if (options == null)
            {
                result.AddError("RecoveryDiagnosticsOptions block cannot be null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(options.ReportsDirectory))
            {
                result.AddError("ReportsDirectory is required.");
            }

            if (options.RetentionLimit <= 0)
            {
                result.AddError("RetentionLimit must be greater than zero.");
            }
        }

        private void ValidateWatchdog(WatchdogOptions options, ConfigurationValidationResult result)
        {
            if (options == null)
            {
                result.AddError("WatchdogOptions block cannot be null.");
                return;
            }

            if (options.PollingInterval <= TimeSpan.Zero)
            {
                result.AddError("Watchdog PollingInterval must be a positive duration.");
            }

            if (options.WorkerHeartbeatTimeout <= TimeSpan.Zero)
            {
                result.AddError("Watchdog WorkerHeartbeatTimeout must be a positive duration.");
            }

            if (options.QueueBacklogWarningThreshold <= 0)
            {
                result.AddError("Watchdog QueueBacklogWarningThreshold must be greater than zero.");
            }
        }
    }
}
