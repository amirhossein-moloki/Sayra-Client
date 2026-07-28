using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents a single tracked cache entry in local update platform storage.
    /// </summary>
    public class CacheEntry
    {
        public string Key { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string EntryType { get; set; } = string.Empty; // 'Package', 'Manifest', 'TemporaryDownload'
        public string Version { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Sha256Hash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public bool IsLocked { get; set; }
        public bool IsValid { get; set; } = true;
    }
}
