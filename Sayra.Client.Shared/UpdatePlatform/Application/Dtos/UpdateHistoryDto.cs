using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Dtos
{
    /// <summary>
    /// Data Transfer Object representing an update history audit entry.
    /// </summary>
    public class UpdateHistoryDto
    {
        /// <summary>
        /// Gets or sets the unique history record ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the system version associated with this action.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the final state status.
        /// </summary>
        public UpdateState State { get; set; }

        /// <summary>
        /// Gets or sets the operation start timestamp.
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Gets or sets the operation completion timestamp, if finished.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the exception details or error context, if failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
