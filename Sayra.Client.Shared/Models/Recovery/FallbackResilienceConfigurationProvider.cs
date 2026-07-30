using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Fallback and test-compatible implementation of IResilienceConfigurationProvider and IPolicyProvider.
    /// Returns default, valid built-in options to prevent null reference or initialization exceptions.
    /// </summary>
    public class FallbackResilienceConfigurationProvider : IResilienceConfigurationProvider, IPolicyProvider
    {
        /// <inheritdoc />
        public ResilienceConfiguration CurrentConfiguration { get; } = CreateDefaultConfiguration();

        /// <inheritdoc />
        public Task UpdateConfigurationAsync(ResilienceConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<RecoveryPolicy> GetPolicyAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            var policy = CurrentConfiguration.SelfHealing.SubsystemPolicies?
                .FirstOrDefault(p => p.SubsystemName.Equals(subsystemName, StringComparison.OrdinalIgnoreCase));

            if (policy != null) return Task.FromResult(policy);

            policy = CurrentConfiguration.RecoveryPolicy.CustomPolicies?
                .FirstOrDefault(p => p.SubsystemName.Equals(subsystemName, StringComparison.OrdinalIgnoreCase));

            if (policy != null) return Task.FromResult(policy);

            var defaultPolicy = new RecoveryPolicy
            {
                SubsystemName = "Default",
                IsEnabled = true,
                Priority = RecoveryPriority.Normal,
                DefaultAction = RecoveryActionType.RestartWorker,
                Retry = new RetryPolicy()
            };

            return Task.FromResult(defaultPolicy);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<RecoveryPolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken = default)
        {
            var list = new List<RecoveryPolicy>();
            if (CurrentConfiguration.SelfHealing.SubsystemPolicies != null)
            {
                list.AddRange(CurrentConfiguration.SelfHealing.SubsystemPolicies);
            }
            return Task.FromResult<IReadOnlyList<RecoveryPolicy>>(list);
        }

        /// <inheritdoc />
        public Task SavePolicyAsync(RecoveryPolicy policy, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        private static ResilienceConfiguration CreateDefaultConfiguration()
        {
            return new ResilienceConfiguration
            {
                SchemaVersion = "1.0.0",
                Description = "Fallback Resilience Profile",
                HealthMonitor = new HealthMonitorOptions(),
                SelfHealing = new SelfHealingOptions
                {
                    IsEnabled = true,
                    MaxAttempts = 5,
                    AttemptsResetDuration = TimeSpan.FromMinutes(10),
                    SubsystemPolicies = new List<RecoveryPolicy>
                    {
                        new()
                        {
                            SubsystemName = "Database",
                            IsEnabled = true,
                            Priority = RecoveryPriority.Critical,
                            DefaultAction = RecoveryActionType.ReconnectDatabase,
                            Retry = new RetryPolicy { MaxRetries = 3, InitialDelay = TimeSpan.FromSeconds(1), BackoffStrategy = BackoffStrategy.ExponentialWithJitter },
                            Cooldown = new CooldownPolicy { CooldownDuration = TimeSpan.FromSeconds(5), EvaluationWindow = TimeSpan.FromSeconds(30), FailureThreshold = 2 }
                        },
                        new()
                        {
                            SubsystemName = "Network",
                            IsEnabled = true,
                            Priority = RecoveryPriority.High,
                            DefaultAction = RecoveryActionType.ReconnectTcp,
                            Retry = new RetryPolicy { MaxRetries = 2, InitialDelay = TimeSpan.FromSeconds(2), BackoffStrategy = BackoffStrategy.Linear }
                        },
                        new()
                        {
                            SubsystemName = "PolicyEngine",
                            IsEnabled = true,
                            Priority = RecoveryPriority.Normal,
                            DefaultAction = RecoveryActionType.ReloadConfiguration,
                            Dependency = new DependencyPolicy
                            {
                                PreRecoveryDependencies = new List<string> { "Database" },
                                FailClosedOnDependencyFailure = true
                            }
                        },
                        new()
                        {
                            SubsystemName = "Default",
                            IsEnabled = true,
                            Priority = RecoveryPriority.Normal,
                            DefaultAction = RecoveryActionType.RestartWorker
                        }
                    }
                },
                RecoveryPolicy = new RecoveryPolicyOptions(),
                CrashRecovery = new CrashRecoveryOptions(),
                ResourceMonitor = new ResourceMonitorOptions(),
                SecurityHardening = new SecurityHardeningOptions(),
                GracefulShutdown = new GracefulShutdownOptions
                {
                    StopWorkTimeout = TimeSpan.FromMilliseconds(5),
                    StopDownloadsTimeout = TimeSpan.FromMilliseconds(5),
                    DrainQueuesTimeout = TimeSpan.FromMilliseconds(5),
                    FlushLogsTimeout = TimeSpan.FromMilliseconds(5),
                    PersistStatesTimeout = TimeSpan.FromMilliseconds(5),
                    StopWorkersTimeout = TimeSpan.FromMilliseconds(5),
                    CloseDatabaseTimeout = TimeSpan.FromMilliseconds(5),
                    OverallTimeout = TimeSpan.FromSeconds(10)
                },
                Diagnostics = new RecoveryDiagnosticsOptions(),
                Watchdog = new WatchdogOptions()
            };
        }
    }
}
