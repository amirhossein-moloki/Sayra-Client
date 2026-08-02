using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Telemetry.Alerts;

namespace Sayra.Client.Shared.Telemetry.Alerts.Evaluators
{
    public class NetworkFailuresRuleEvaluator : IAlertRuleEvaluator
    {
        private readonly IAlertDiagnosticsCache _cache;
        private readonly IAlertPolicyProvider _policyProvider;

        public NetworkFailuresRuleEvaluator(IAlertDiagnosticsCache cache, IAlertPolicyProvider policyProvider)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        }

        public string RuleName => "NetworkFailures";
        public string Subsystem => "Network";

        public async Task<AlertRecord?> EvaluateAsync(CancellationToken cancellationToken = default)
        {
            var report = await _cache.GetLatestReportAsync(cancellationToken);
            var policy = await _policyProvider.GetPolicyAsync(RuleName, cancellationToken);

            bool hasFailure = report.Errors.Any(e => e.Contains("Network", StringComparison.OrdinalIgnoreCase)) ||
                              (report.SubsystemStatus.TryGetValue("Network", out var status) &&
                               (status.Equals("Error", StringComparison.OrdinalIgnoreCase) || status.Equals("Unknown", StringComparison.OrdinalIgnoreCase)));

            if (hasFailure)
            {
                Enum.TryParse<AlertPriority>(policy.Evaluation.DefaultPriority, out var priority);
                return new AlertRecord
                {
                    Name = RuleName,
                    Subsystem = SubsystemType.Network,
                    Category = MetricCategory.Network,
                    Priority = priority == default ? AlertPriority.Warning : priority,
                    Value = 1.0,
                    Threshold = 0.0,
                    Message = "Workstation network connectivity failure or packet loss detected"
                };
            }

            return null;
        }
    }
}
