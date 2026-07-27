using System;

namespace Sayra.Client.Shared.Models
{
    public class PolicyVersion
    {
        public long VersionCode { get; set; }
        public DateTime IssuedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
