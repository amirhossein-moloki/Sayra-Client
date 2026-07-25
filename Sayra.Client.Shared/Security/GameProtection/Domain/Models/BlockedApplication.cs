namespace Sayra.Client.Shared.Security.GameProtection.Domain.Models;

public class BlockedApplication
{
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Severity { get; set; } = "High";
}
