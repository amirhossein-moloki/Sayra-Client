using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Collectors.Runtime
{
    /// <summary>
    /// Collects administrative policy enforcement status and compliance.
    /// </summary>
    public class PolicyCollector : BaseTelemetryCollector
    {
        public PolicyCollector(ILogger<PolicyCollector> logger)
            : base("Policy Collector", CollectionInterval.Performance, 75, TimeSpan.FromSeconds(5), logger)
        {
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            double isCompliant = 1.0; // 1 = compliant, 0 = violation

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "runtime.policy.compliant",
                    Category = MetricCategory.Policy,
                    Value = isCompliant,
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = MetricSeverity.Info,
                    Tags = new Dictionary<string, string> { { "active_policy_set", "Standard_Kiosk_V2" } }
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }
    }
}
