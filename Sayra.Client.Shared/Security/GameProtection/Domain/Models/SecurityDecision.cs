using Sayra.Client.Shared.Security.GameProtection.Domain.Rules;

namespace Sayra.Client.Shared.Security.GameProtection.Domain.Models;

public class SecurityDecision
{
    public ProcessAction Action { get; set; } = ProcessAction.Allow;
    public string Reason { get; set; } = string.Empty;
    public string Severity { get; set; } = "Low";
    public ProcessRule? RuleTriggered { get; set; }
}
