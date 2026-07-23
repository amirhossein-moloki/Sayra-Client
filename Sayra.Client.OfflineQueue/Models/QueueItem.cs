using System;

namespace Sayra.Client.OfflineQueue.Models;

public class QueueItem
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public QueuePriority Priority { get; set; } = QueuePriority.NORMAL;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Processing, Failed, Completed
    public DateTime? LastAttemptAt { get; set; }
    public string? ErrorMessage { get; set; }
}
