using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents an active or completed rollback transaction job.
    /// </summary>
    public class RollbackJob
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string SnapshotId { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
        public RollbackStatus Status { get; set; } = RollbackStatus.Started;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
