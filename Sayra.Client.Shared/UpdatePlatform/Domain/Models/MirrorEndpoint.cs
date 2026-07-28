using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents a CDN or mirror endpoint with health and priority.
    /// </summary>
    public class MirrorEndpoint
    {
        public string Name { get; set; } = string.Empty;
        public Uri BaseUri { get; set; } = null!;
        public int Priority { get; set; } = 1; // Lower values mean higher priority
        public bool IsHealthy { get; set; } = true;
        public TimeSpan LastLatency { get; set; } = TimeSpan.Zero;
        public DateTime LastCheckedUtc { get; set; } = DateTime.MinValue;
        public int FailureCount { get; set; }
    }
}
