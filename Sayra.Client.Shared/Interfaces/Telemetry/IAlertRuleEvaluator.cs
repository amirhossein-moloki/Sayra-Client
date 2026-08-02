using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Contract for an independently executable alert rule evaluator.
    /// </summary>
    public interface IAlertRuleEvaluator
    {
        /// <summary>
        /// Gets the identifying name of the rule.
        /// </summary>
        string RuleName { get; }

        /// <summary>
        /// Gets the associated subsystem.
        /// </summary>
        string Subsystem { get; }

        /// <summary>
        /// Asynchronously evaluates the rule against the current state.
        /// Returns an AlertRecord if a breach is detected; otherwise, null.
        /// </summary>
        Task<AlertRecord?> EvaluateAsync(CancellationToken cancellationToken = default);
    }
}
