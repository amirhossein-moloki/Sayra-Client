using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Alerts.Evaluators
{
    public class CpuThresholdRuleEvaluator : IAlertRuleEvaluator
    {
        private readonly ILiveTelemetryService _liveTelemetry;
        private readonly IAlertPolicyProvider _policyProvider;

        public CpuThresholdRuleEvaluator(ILiveTelemetryService liveTelemetry, IAlertPolicyProvider policyProvider)
        {
            _liveTelemetry = liveTelemetry ?? throw new ArgumentNullException(nameof(liveTelemetry));
            _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        }

        public string RuleName => "CpuThreshold";
        public string Subsystem => "Telemetry";

        public async Task<AlertRecord?> EvaluateAsync(CancellationToken cancellationToken = default)
        {
            var telemetry = await _liveTelemetry.CaptureSnapshotAsync(cancellationToken);
            var policy = await _policyProvider.GetPolicyAsync(RuleName, cancellationToken);

            double cpuUsage = telemetry.CpuUsagePercent;
            double threshold = policy.Threshold.Value ?? 90.0;

            if (cpuUsage > threshold)
            {
                Enum.TryParse<AlertPriority>(policy.Evaluation.DefaultPriority, out var priority);
                return new AlertRecord
                {
                    Name = RuleName,
                    Subsystem = SubsystemType.Telemetry,
                    Category = MetricCategory.Cpu,
                    Priority = priority == default ? AlertPriority.Warning : priority,
                    Value = cpuUsage,
                    Threshold = threshold,
                    Message = $"CPU utilization is critically high at {cpuUsage:F1}% (threshold: {threshold:F1}%)"
                };
            }

            return null;
        }
    }
}
