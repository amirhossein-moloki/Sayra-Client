using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Recovery
{
    public class SubsystemHealthInfo
    {
        public string SubsystemName { get; set; } = string.Empty;
        public SubsystemHealthState State { get; set; } = SubsystemHealthState.Healthy;
        public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
        public List<string> Dependencies { get; set; } = new();
        public List<string> HealthHistory { get; set; } = new();
        public string LastMessage { get; set; } = string.Empty;
        public string? LastException { get; set; }
    }
}
