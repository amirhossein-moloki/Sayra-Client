using System;

namespace Sayra.Client.OfflineQueue.Models;

public class ClientEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty;
    public string EventVersion { get; set; } = "1.0.0";
    public string ClientId { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public QueuePriority Priority { get; set; } = QueuePriority.NORMAL;
    public string Payload { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// Validates the core required properties of the event.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EventId))
            throw new ArgumentException("EventId is required.", nameof(EventId));
        if (string.IsNullOrWhiteSpace(EventType))
            throw new ArgumentException("EventType is required.", nameof(EventType));
        if (string.IsNullOrWhiteSpace(EventVersion))
            throw new ArgumentException("EventVersion is required.", nameof(EventVersion));
        if (string.IsNullOrWhiteSpace(Payload))
            throw new ArgumentException("Payload is required.", nameof(Payload));
    }
}
