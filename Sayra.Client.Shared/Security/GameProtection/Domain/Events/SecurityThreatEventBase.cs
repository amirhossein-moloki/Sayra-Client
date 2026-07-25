using System;

namespace Sayra.Client.Shared.Security.GameProtection.Domain.Events;

public abstract class SecurityThreatEventBase
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string Severity { get; set; } = "Medium";
    public string Reason { get; set; } = string.Empty;
}
