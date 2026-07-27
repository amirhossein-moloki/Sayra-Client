using System;

namespace Sayra.Client.Shared.Models
{
    public class DynamicCollection
    {
        public string CollectionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RuleJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
