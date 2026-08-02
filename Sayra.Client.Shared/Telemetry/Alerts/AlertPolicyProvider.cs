using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Models.Telemetry.Policies;

namespace Sayra.Client.Shared.Telemetry.Alerts
{
    public class AlertPolicyProvider : IAlertPolicyProvider
    {
        private readonly AlertOptions _options;

        public AlertPolicyProvider(IOptions<AlertOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public Task<AlertPolicyConfig> GetPolicyAsync(string ruleName, CancellationToken cancellationToken = default)
        {
            if (_options.Rules != null && _options.Rules.TryGetValue(ruleName, out var config))
            {
                return Task.FromResult(config);
            }

            var defaultConfig = CreateDefaultConfig(ruleName);
            return Task.FromResult(defaultConfig);
        }

        private AlertPolicyConfig CreateDefaultConfig(string ruleName)
        {
            var config = new AlertPolicyConfig
            {
                Threshold = new ThresholdPolicy { Operator = "GreaterThan", Value = 90.0 },
                Suppression = new SuppressionPolicy(),
                Escalation = new EscalationPolicy
                {
                    Enabled = true,
                    DurationMinutesBeforeEscalation = 5,
                    FrequencyThreshold = 3,
                    EscalationPriority = "Critical"
                },
                Recovery = new RecoveryPolicy
                {
                    AutoResolve = true,
                    ConsecutiveNormalSamplesRequired = 2
                },
                RateLimit = new RateLimitPolicy(),
                Evaluation = new EvaluationPolicy { Enabled = true, IntervalSeconds = 15, DefaultPriority = "Warning" }
            };

            switch (ruleName)
            {
                case "CpuThreshold":
                    config.Threshold.Value = _options.CpuThresholdPercent;
                    config.Recovery.RecoveryThreshold = _options.CpuThresholdPercent;
                    config.Recovery.RecoveryOperator = "LessThan";
                    break;
                case "MemoryThreshold":
                    config.Threshold.Value = _options.MemoryThresholdPercent;
                    config.Recovery.RecoveryThreshold = _options.MemoryThresholdPercent;
                    config.Recovery.RecoveryOperator = "LessThan";
                    break;
                case "DiskUsage":
                    config.Threshold.Operator = "LessThan";
                    config.Threshold.Value = _options.DiskFreeSpaceThresholdPercent;
                    config.Recovery.RecoveryThreshold = _options.DiskFreeSpaceThresholdPercent;
                    config.Recovery.RecoveryOperator = "GreaterThan";
                    break;
                case "NetworkFailures":
                case "DatabaseFailures":
                case "IpcFailures":
                case "DownloadFailures":
                case "UpdateFailures":
                case "PluginFailures":
                case "SecurityFailures":
                case "PolicyViolations":
                case "RuntimeFailures":
                case "ConfigurationFailures":
                    config.Threshold.Operator = "Boolean";
                    config.Threshold.BooleanValue = true;
                    config.Recovery.AutoResolve = true;
                    break;
            }

            return config;
        }
    }
}
