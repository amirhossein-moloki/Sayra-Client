using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the runtime session status of a download pipeline.
    /// </summary>
    public class DownloadSession
    {
        public Guid SessionId { get; set; } = Guid.NewGuid();
        public Guid JobId { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public string ActiveMirrorName { get; set; } = string.Empty;
        public int ActiveWorkers { get; set; }
    }
}
