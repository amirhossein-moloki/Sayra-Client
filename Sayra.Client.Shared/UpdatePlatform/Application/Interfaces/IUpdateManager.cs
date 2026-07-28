using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Dtos;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Represents the high-level orchestration service for the update and deployment platform.
    /// </summary>
    public interface IUpdateManager
    {
        /// <summary>
        /// Gets the current state of the update lifecycle.
        /// </summary>
        UpdateState GetCurrentState();

        /// <summary>
        /// Evaluates and checks for any newly published client update.
        /// </summary>
        /// <param name="request">The parameters of the workstation and channel.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A response containing whether an update is available and manifest details.</returns>
        Task<UpdateCheckResponseDto> CheckForUpdatesAsync(UpdateCheckRequestDto request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts and executes the update process for a given manifest.
        /// </summary>
        /// <param name="manifest">The update manifest configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the update successfully completed; otherwise, false.</returns>
        Task<bool> StartUpdateAsync(UpdateManifest manifest, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels the currently running update process.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CancelUpdateAsync(CancellationToken cancellationToken = default);
    }
}
