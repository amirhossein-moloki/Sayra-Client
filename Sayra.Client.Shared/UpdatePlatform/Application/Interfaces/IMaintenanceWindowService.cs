using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Contract managing enterprise maintenance window checks and operations.
    /// </summary>
    public interface IMaintenanceWindowService
    {
        /// <summary>
        /// Checks if the specified time is within the allowed maintenance window.
        /// </summary>
        bool IsInsideWindow(DateTime timeToCheck);

        /// <summary>
        /// Gets the next scheduled date/time when a maintenance window opens.
        /// </summary>
        DateTime GetNextWindowStart(DateTime currentTime);

        /// <summary>
        /// Enforces maintenance window compliance, throwing a MaintenanceWindowViolationException if violated.
        /// </summary>
        void EnsureInsideWindow(DateTime timeToCheck, bool overrideForForced = false);

        /// <summary>
        /// Retrieves the currently active maintenance window configuration.
        /// </summary>
        MaintenanceWindow GetMaintenanceWindow();
    }
}
