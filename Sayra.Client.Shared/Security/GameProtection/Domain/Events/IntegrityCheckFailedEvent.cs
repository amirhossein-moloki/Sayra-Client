using System;

namespace Sayra.Client.Shared.Security.GameProtection.Domain.Events;

public class IntegrityCheckFailedEvent : SecurityThreatEventBase
{
    public string FilePath { get; set; } = string.Empty;
    public string ExpectedHash { get; set; } = string.Empty;
    public string ActualHash { get; set; } = string.Empty;

    public IntegrityCheckFailedEvent()
    {
        Severity = "Critical";
    }
}
