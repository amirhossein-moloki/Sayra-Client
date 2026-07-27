using System;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Dtos
{
    /// <summary>
    /// Data Transfer Object representing an administrator-requested workstation version rollback trigger.
    /// </summary>
    public class RollbackRequestDto
    {
        /// <summary>
        /// Gets or sets the target system version to restore.
        /// </summary>
        public string RollbackVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the formal reason for initiating the system rollback.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to execute the rollback immediately, bypassing active locks.
        /// </summary>
        public bool Force { get; set; }
    }
}
