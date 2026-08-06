namespace Sayra.Client.Shared.GameDistribution.Cache.Models
{
    public class BlockAvailability
    {
        public string NodeId { get; set; } = string.Empty;
        public string BlockId { get; set; } = string.Empty;
        public string GameId { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }
}
