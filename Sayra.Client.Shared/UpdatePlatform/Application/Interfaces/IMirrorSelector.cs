using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Manages multiple mirrors and CDN endpoints, prioritizes them based on latency and health, and supports automatic failover.
    /// </summary>
    public interface IMirrorSelector
    {
        /// <summary>
        /// Retrieves the list of currently registered mirror endpoints.
        /// </summary>
        IReadOnlyList<MirrorEndpoint> GetEndpoints();

        /// <summary>
        /// Registers a new mirror endpoint.
        /// </summary>
        void RegisterEndpoint(MirrorEndpoint endpoint);

        /// <summary>
        /// Selects the best healthy mirror endpoint based on priority and latency.
        /// </summary>
        /// <returns>The chosen MirrorEndpoint.</returns>
        /// <exception cref="Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions.MirrorUnavailableException">Thrown when no healthy mirrors are available.</exception>
        MirrorEndpoint GetBestEndpoint();

        /// <summary>
        /// Reports a connection failure for the specified mirror endpoint, triggering potential failover.
        /// </summary>
        /// <param name="endpoint">The failed mirror endpoint.</param>
        void ReportFailure(MirrorEndpoint endpoint);

        /// <summary>
        /// Initiates background latency and health checks on all mirrors.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task ProbeHealthAsync(CancellationToken cancellationToken = default);
    }
}
