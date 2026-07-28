using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the outcome of a system rollback transaction.
    /// </summary>
    public class RollbackResult
    {
        public bool Succeeded { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int RestoredFilesCount { get; set; }
        public string RestoredVersion { get; set; } = string.Empty;
    }
}
