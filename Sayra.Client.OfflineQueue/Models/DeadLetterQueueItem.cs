using System;

namespace Sayra.Client.OfflineQueue.Models;

public class DeadLetterQueueItem
{
    public long Id { get; set; }
    public long OriginalQueueItemId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public QueuePriority Priority { get; set; } = QueuePriority.NORMAL;
    public string ErrorReason { get; set; } = string.Empty;
    public string RetryHistory { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
