using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Alerts
{
    public class AlertRuleProvider : IAlertRuleProvider
    {
        private readonly IEnumerable<IAlertRuleEvaluator> _evaluators;

        public AlertRuleProvider(IEnumerable<IAlertRuleEvaluator> evaluators)
        {
            _evaluators = evaluators ?? throw new ArgumentNullException(nameof(evaluators));
        }

        public Task<IReadOnlyCollection<IAlertRuleEvaluator>> GetRuleEvaluatorsAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<IAlertRuleEvaluator> list = _evaluators.ToList();
            return Task.FromResult(list);
        }
    }
}
