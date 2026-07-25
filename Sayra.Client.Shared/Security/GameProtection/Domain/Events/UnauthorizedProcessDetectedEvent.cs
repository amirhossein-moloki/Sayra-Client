using System;

namespace Sayra.Client.Shared.Security.GameProtection.Domain.Events;

public class UnauthorizedProcessDetectedEvent : SecurityThreatEventBase
{
    public string ExecutablePath { get; set; } = string.Empty;

    public UnauthorizedProcessDetectedEvent()
    {
        Severity = "High";
    }
}
