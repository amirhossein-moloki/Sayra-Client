using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Represents the core transactional engine responsible for installing verified update packages.
    /// </summary>
    public interface IInstallerEngine
    {
        /// <summary>
        /// Installs the files from the update package onto the workstation.
        /// </summary>
        /// <param name="package">The package to install.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the installation succeeded; otherwise, false.</returns>
        Task<bool> InstallAsync(UpdatePackage package, CancellationToken cancellationToken = default);
    }
}
