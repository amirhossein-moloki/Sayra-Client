using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Dtos
{
    /// <summary>
    /// Data Transfer Object representing the real-time background update status.
    /// </summary>
    public class UpdateStatusDto
    {
        /// <summary>
        /// Gets or sets the current state machine execution status.
        /// </summary>
        public UpdateState CurrentState { get; set; }

        /// <summary>
        /// Gets or sets the overall update action progress percentage (0.0 to 100.0).
        /// </summary>
        public double ProgressPercentage { get; set; }

        /// <summary>
        /// Gets or sets the description of the active background installation step.
        /// </summary>
        public string CurrentAction { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets any error description if the update failed or was cancelled.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
