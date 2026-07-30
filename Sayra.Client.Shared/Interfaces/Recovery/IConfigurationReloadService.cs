using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Contract for triggering dynamic runtime reload and atomic application of resilience configurations.
    /// </summary>
    public interface IConfigurationReloadService
    {
        /// <summary>
        /// Triggers a dynamic reload of the resilience configuration from JSON storage.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the reload and validation passed and was applied successfully; otherwise false.</returns>
        Task<bool> ReloadAsync(CancellationToken cancellationToken = default);
    }
}
