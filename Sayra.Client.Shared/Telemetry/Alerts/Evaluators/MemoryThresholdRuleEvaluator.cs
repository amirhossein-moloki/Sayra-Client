using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Alerts.Evaluators
{
    public class MemoryThresholdRuleEvaluator : IAlertRuleEvaluator
    {
        private readonly ILiveTelemetryService _liveTelemetry;
        private readonly IAlertPolicyProvider _policyProvider;

        public MemoryThresholdRuleEvaluator(ILiveTelemetryService liveTelemetry, IAlertPolicyProvider policyProvider)
        {
            _liveTelemetry = liveTelemetry ?? throw new ArgumentNullException(nameof(liveTelemetry));
            _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        }

        public string RuleName => "MemoryThreshold";
        public string Subsystem => "Telemetry";

        public async Task<AlertRecord?> EvaluateAsync(CancellationToken cancellationToken = default)
        {
            var telemetry = await _liveTelemetry.CaptureSnapshotAsync(cancellationToken);
            var policy = await _policyProvider.GetPolicyAsync(RuleName, cancellationToken);

            double memUsage = telemetry.RamTotalMb > 0 ? (telemetry.RamUsedMb / telemetry.RamTotalMb) * 100 : 0;
            double threshold = policy.Threshold.Value ?? 90.0;

            if (memUsage > threshold)
            {
                Enum.TryParse<AlertPriority>(policy.Evaluation.DefaultPriority, out var priority);
                return new AlertRecord
                {
                    Name = RuleName,
                    Subsystem = SubsystemType.Telemetry,
                    Category = MetricCategory.Memory,
                    Priority = priority == default ? AlertPriority.Warning : priority,
                    Value = memUsage,
                    Threshold = threshold,
                    Message = $"Memory usage is critically high at {memUsage:F1}% (threshold: {threshold:F1}%)"
                };
            }

            return null;
        }
    }
}
