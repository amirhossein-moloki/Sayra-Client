using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents a strongly-typed telemetry event capturing update lifecycle status and diagnostic metadata.
    /// </summary>
    public class UpdateTelemetryEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public string EventType { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string CorrelationId { get; set; } = string.Empty;
        public string SourceVersion { get; set; } = string.Empty;
        public string TargetVersion { get; set; } = string.Empty;
        public bool Success { get; set; } = true;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string DeviceIdentifier { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
    }
}
