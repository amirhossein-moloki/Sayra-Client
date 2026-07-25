using System;

namespace Sayra.Client.Shared.Security.GameProtection.Domain.Events;

public class TamperingDetectedEvent : SecurityThreatEventBase
{
    public string TargetComponent { get; set; } = string.Empty; // e.g. "Configuration", "Executable", "Memory"

    public TamperingDetectedEvent()
    {
        Severity = "Critical";
    }
}
