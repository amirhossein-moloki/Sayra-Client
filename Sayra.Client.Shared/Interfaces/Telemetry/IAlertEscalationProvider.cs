using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Contract for dynamically escalating active alerts based on frequency, duration, recurrence, and severity.
    /// </summary>
    public interface IAlertEscalationProvider
    {
        /// <summary>
        /// Evaluates and applies escalation to an active alert if threshold criteria are breached.
        /// </summary>
        Task<AlertRecord?> CheckAndEscalateAsync(AlertRecord alert, AlertPolicyConfig policy, CancellationToken cancellationToken = default);
    }
}
