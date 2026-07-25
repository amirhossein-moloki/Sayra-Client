using System;

namespace Sayra.Client.Shared.Security.GameProtection.Domain.Events;

public class BlockedApplicationDetectedEvent : SecurityThreatEventBase
{
    public string RulePatternMatched { get; set; } = string.Empty;

    public BlockedApplicationDetectedEvent()
    {
        Severity = "High";
    }
}
