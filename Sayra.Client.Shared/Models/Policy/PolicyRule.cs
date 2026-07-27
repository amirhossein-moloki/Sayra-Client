using System.Collections.Generic;

namespace Sayra.Client.Shared.Models
{
    public class PolicyRule
    {
        public string RuleId { get; set; } = string.Empty;
        public PolicyCategory Category { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new();
    }
}
