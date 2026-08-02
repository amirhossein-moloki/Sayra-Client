using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Alerts.Evaluators
{
    public class DiskUsageRuleEvaluator : IAlertRuleEvaluator
    {
        private readonly ILiveTelemetryService _liveTelemetry;
        private readonly IAlertPolicyProvider _policyProvider;

        public DiskUsageRuleEvaluator(ILiveTelemetryService liveTelemetry, IAlertPolicyProvider policyProvider)
        {
            _liveTelemetry = liveTelemetry ?? throw new ArgumentNullException(nameof(liveTelemetry));
            _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        }

        public string RuleName => "DiskUsage";
        public string Subsystem => "Telemetry";

        public async Task<AlertRecord?> EvaluateAsync(CancellationToken cancellationToken = default)
        {
            var telemetry = await _liveTelemetry.CaptureSnapshotAsync(cancellationToken);
            var policy = await _policyProvider.GetPolicyAsync(RuleName, cancellationToken);

            double freeSpace = telemetry.FreeSpaceGb;
            double threshold = policy.Threshold.Value ?? 10.0; // Free space threshold, e.g. 10 GB

            if (freeSpace < threshold)
            {
                Enum.TryParse<AlertPriority>(policy.Evaluation.DefaultPriority, out var priority);
                return new AlertRecord
                {
                    Name = RuleName,
                    Subsystem = SubsystemType.Telemetry,
                    Category = MetricCategory.Disk,
                    Priority = priority == default ? AlertPriority.Warning : priority,
                    Value = freeSpace,
                    Threshold = threshold,
                    Message = $"Disk free space is low at {freeSpace:F1} GB (threshold: {threshold:F1} GB)"
                };
            }

            return null;
        }
    }
}
