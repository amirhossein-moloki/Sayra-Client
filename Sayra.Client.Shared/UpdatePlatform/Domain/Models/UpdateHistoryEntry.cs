using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents a historical entry of an update operation on this workstation.
    /// </summary>
    public class UpdateHistoryEntry
    {
        /// <summary>
        /// Gets or sets the unique identifier of this history record.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the version applied in this operation.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the end state of this update operation.
        /// </summary>
        public UpdateState State { get; set; }

        /// <summary>
        /// Gets or sets the UTC start time.
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Gets or sets the UTC completion time, if finished.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets any error message raised during execution.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
