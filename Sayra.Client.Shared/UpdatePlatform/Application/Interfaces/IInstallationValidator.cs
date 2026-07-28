using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Performs post-installation validation of file integrity, manifest consistency, and version correctness.
    /// </summary>
    public interface IInstallationValidator
    {
        /// <summary>
        /// Validates the installation integrity after the files are applied.
        /// </summary>
        /// <param name="context">The installation context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the installation is valid and consistent; otherwise, false.</returns>
        Task<bool> ValidateAsync(InstallationContext context, CancellationToken cancellationToken = default);
    }
}
