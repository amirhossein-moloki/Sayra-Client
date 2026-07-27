using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents a historical log of a system rollback recovery procedure.
    /// </summary>
    public class RollbackRecord
    {
        /// <summary>
        /// Gets or sets the unique rollback transaction identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the target failed version that was rolled back.
        /// </summary>
        public string UpdateVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the status of the rollback recovery.
        /// </summary>
        public RollbackStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the UTC creation time.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the UTC completion time, if finished.
        /// </summary>
        public DateTime? CompletedAt { get; set; }
    }
}
