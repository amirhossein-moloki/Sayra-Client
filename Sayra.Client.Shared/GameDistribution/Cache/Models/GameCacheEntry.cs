using System;

namespace Sayra.Client.Shared.GameDistribution.Cache.Models
{
    public class GameCacheEntry
    {
        public string GameId { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string PackageId { get; set; } = string.Empty;
        public int TotalBlocks { get; set; }
        public int CompletedBlocks { get; set; }
        public long TotalSize { get; set; }
        public bool IsHealthy { get; set; }
        public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;
    }
}
