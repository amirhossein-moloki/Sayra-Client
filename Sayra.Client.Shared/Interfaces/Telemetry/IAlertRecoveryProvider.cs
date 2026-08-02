using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Contract for automatic recovery detection and resolution of active alerts.
    /// </summary>
    public interface IAlertRecoveryProvider
    {
        /// <summary>
        /// Evaluates if an active alert's condition has recovered.
        /// </summary>
        Task<bool> EvaluateRecoveryAsync(AlertRecord activeAlert, AlertPolicyConfig policy, CancellationToken cancellationToken = default);
    }
}
