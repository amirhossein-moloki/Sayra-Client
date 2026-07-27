using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models
{
    public class PolicyChangeResult
    {
        public bool Success { get; set; }
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
        public List<string> ModifiedRules { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
