using System;

namespace Sayra.Client.Shared.Models
{
    public class MachineGroup
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDynamic { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ParentGroupId { get; set; }
    }
}
