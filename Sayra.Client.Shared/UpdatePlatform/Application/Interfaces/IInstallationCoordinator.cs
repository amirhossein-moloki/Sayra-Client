using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Coordinates the full installation pipeline (staging, service stopping, atomic replacement, validation, and restarts).
    /// </summary>
    public interface IInstallationCoordinator
    {
        /// <summary>
        /// Executes the structured installation job.
        /// </summary>
        /// <param name="job">The installation job definitions.</param>
        /// <param name="progress">The granular progress reporter.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An installation result denoting success or failure.</returns>
        Task<InstallationResult> CoordinateAsync(InstallationJob job, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    }
}
