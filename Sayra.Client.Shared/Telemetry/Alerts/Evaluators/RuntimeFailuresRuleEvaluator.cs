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
    public class RuntimeFailuresRuleEvaluator : IAlertRuleEvaluator
    {
        private readonly IAlertDiagnosticsCache _cache;
        private readonly IAlertPolicyProvider _policyProvider;

        public RuntimeFailuresRuleEvaluator(IAlertDiagnosticsCache cache, IAlertPolicyProvider policyProvider)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        }

        public string RuleName => "RuntimeFailures";
        public string Subsystem => "Telemetry";

        public async Task<AlertRecord?> EvaluateAsync(CancellationToken cancellationToken = default)
        {
            var report = await _cache.GetLatestReportAsync(cancellationToken);
            var policy = await _policyProvider.GetPolicyAsync(RuleName, cancellationToken);

            bool hasFailure = report.Errors.Any(e => e.Contains("Runtime", StringComparison.OrdinalIgnoreCase) || e.Contains("ThreadPool", StringComparison.OrdinalIgnoreCase) || e.Contains("Worker", StringComparison.OrdinalIgnoreCase)) ||
                              (report.SubsystemStatus.TryGetValue("Telemetry", out var status) &&
                               (status.Equals("Error", StringComparison.OrdinalIgnoreCase) || status.Equals("Unknown", StringComparison.OrdinalIgnoreCase)));

            if (hasFailure)
            {
                Enum.TryParse<AlertPriority>(policy.Evaluation.DefaultPriority, out var priority);
                return new AlertRecord
                {
                    Name = RuleName,
                    Subsystem = SubsystemType.Telemetry,
                    Category = MetricCategory.Watchdog,
                    Priority = priority == default ? AlertPriority.Warning : priority,
                    Value = 1.0,
                    Threshold = 0.0,
                    Message = "Runtime .NET ThreadPool worker thread starvation or hosted supervisor exception detected"
                };
            }

            return null;
        }
    }
}
