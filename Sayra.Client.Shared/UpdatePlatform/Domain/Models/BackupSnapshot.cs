using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents metadata for a packaged system backup snapshot.
    /// </summary>
    public class BackupSnapshot
    {
        public string BackupId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Sha256Hash { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public bool IsValid { get; set; }
    }
}
