using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Recovery;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Provides atomic thread-safe access and administrative updates to the centralized resilience configuration.
    /// </summary>
    public interface IResilienceConfigurationProvider
    {
        /// <summary>
        /// Gets the current active, validated resilience configuration.
        /// </summary>
        ResilienceConfiguration CurrentConfiguration { get; }

        /// <summary>
        /// Atomically updates and persists the resilience configuration profile.
        /// </summary>
        /// <param name="configuration">The new configuration to apply.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task UpdateConfigurationAsync(ResilienceConfiguration configuration, CancellationToken cancellationToken = default);
    }
}
