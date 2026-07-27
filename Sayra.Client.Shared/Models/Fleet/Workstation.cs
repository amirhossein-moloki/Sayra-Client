using System;

namespace Sayra.Client.Shared.Models
{
    public class Workstation
    {
        public string WorkstationId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string Status { get; set; } = "Offline"; // Offline, Online, Maintenance
        public string LastSeen { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Gpu { get; set; } = string.Empty;
        public int RamGb { get; set; }
        public string WindowsVersion { get; set; } = string.Empty;
        public string PolicyVersion { get; set; } = string.Empty;
        public string HealthState { get; set; } = "Healthy"; // Healthy, Warning, Critical
    }
}
