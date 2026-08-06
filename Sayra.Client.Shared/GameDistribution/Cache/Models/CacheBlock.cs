namespace Sayra.Client.Shared.GameDistribution.Cache.Models
{
    public class CacheBlock
    {
        public string BlockId { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Sha256Hash { get; set; } = string.Empty;
        public bool IsStored { get; set; }
        public string LocalPath { get; set; } = string.Empty;
    }
}
