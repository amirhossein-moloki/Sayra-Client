using System;

namespace Sayra.Client.Shared.Models
{
    public class GroupAssignment
    {
        public string AssignmentId { get; set; } = string.Empty;
        public string MachineId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
