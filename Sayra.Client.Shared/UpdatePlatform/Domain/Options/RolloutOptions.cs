using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Options
{
    /// <summary>
    /// Configuration options for Rollout management and evaluation.
    /// </summary>
    public class RolloutOptions
    {
        public int RolloutPercentage { get; set; } = 100;
        public bool IsPaused { get; set; }
        public bool IsCancelled { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public List<string> ExcludedDeviceIds { get; set; } = new();
    }
}
