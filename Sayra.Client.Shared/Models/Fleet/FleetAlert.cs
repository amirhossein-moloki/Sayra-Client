using System;

namespace Sayra.Client.Shared.Models
{
    public class FleetAlert
    {
        public string AlertId { get; set; } = string.Empty;
        public string WorkstationId { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty; // e.g. GPU_TEMP, CPU_TEMP, DISK_FULL, etc.
        public string Severity { get; set; } = "Warning"; // Critical, Warning, Information
        public string Message { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string ResolvedAt { get; set; } = string.Empty;
        public string CooldownExpiresAt { get; set; } = string.Empty;
        public int Escalated { get; set; } // 0 or 1
        public int IsActive { get; set; } = 1; // 0 or 1
    }
}
