using System;

namespace Sayra.Client.Shared.Runtime.Overlay.Domain.Models
{
    /// <summary>
    /// Represents the UI-independent data payload displayed on the game runtime overlay.
    /// </summary>
    public class OverlayData
    {
        public Guid SessionId { get; set; }
        public TimeSpan RemainingTime { get; set; }
        public string SessionState { get; set; } = string.Empty;
        public int WarningLevel { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool Visibility { get; set; }
    }
}
