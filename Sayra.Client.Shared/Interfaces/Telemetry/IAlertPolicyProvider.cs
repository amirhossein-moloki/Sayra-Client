using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Contract for retrieving the active alert policies and configurations for rules.
    /// </summary>
    public interface IAlertPolicyProvider
    {
        /// <summary>
        /// Retrieves the alert policy configuration for the specified rule name.
        /// </summary>
        Task<AlertPolicyConfig> GetPolicyAsync(string ruleName, CancellationToken cancellationToken = default);
    }
}
