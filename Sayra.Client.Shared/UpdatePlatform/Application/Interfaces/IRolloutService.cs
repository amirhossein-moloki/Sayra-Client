using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Contract governing staged update rollout and progressive client exposure.
    /// </summary>
    public interface IRolloutService
    {
        /// <summary>
        /// Evaluates whether a client device is selected to receive the update based on staged rollout percentages.
        /// </summary>
        bool IsDeviceEligibleForRollout(string deviceId, RolloutConfiguration config);

        /// <summary>
        /// Pauses the current rollout configuration.
        /// </summary>
        void PauseRollout(RolloutConfiguration config);

        /// <summary>
        /// Resumes a paused rollout configuration.
        /// </summary>
        void ResumeRollout(RolloutConfiguration config);

        /// <summary>
        /// Cancels a rollout configuration entirely.
        /// </summary>
        void CancelRollout(RolloutConfiguration config);
    }
}
