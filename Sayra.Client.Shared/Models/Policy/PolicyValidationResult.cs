using System.Collections.Generic;

namespace Sayra.Client.Shared.Models
{
    public class PolicyValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
