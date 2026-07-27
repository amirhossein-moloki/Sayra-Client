using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models
{
    public class PolicyProfile
    {
        public string PolicyId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long Version { get; set; }
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public string Signature { get; set; } = string.Empty;
        public List<PolicyRule> Rules { get; set; } = new();
    }
}
