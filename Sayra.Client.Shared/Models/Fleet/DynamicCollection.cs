using System;

namespace Sayra.Client.Shared.Models
{
    public class DynamicCollection
    {
        public string CollectionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RuleExpression { get; set; } = string.Empty;
        public string LastUpdatedAt { get; set; } = string.Empty;
    }
}
