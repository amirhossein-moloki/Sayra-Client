using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Contract for obtaining all active, dynamically registered alert rule evaluators.
    /// </summary>
    public interface IAlertRuleProvider
    {
        /// <summary>
        /// Retrieves the list of all registered rule evaluators.
        /// </summary>
        Task<IReadOnlyCollection<IAlertRuleEvaluator>> GetRuleEvaluatorsAsync(CancellationToken cancellationToken = default);
    }
}
