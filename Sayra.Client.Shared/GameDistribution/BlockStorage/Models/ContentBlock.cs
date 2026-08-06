using System;

namespace Sayra.Client.Shared.GameDistribution.BlockStorage.Models
{
    public class ContentBlock
    {
        public string BlockId { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Sha256Hash { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string GameId { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public DateTime CreationTime { get; set; } = DateTime.UtcNow;
    }
}
