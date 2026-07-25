namespace Sayra.Client.Shared.Security.GameProtection.Domain.Models;

public class AllowedGame
{
    public string GameId { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
    public string ExpectedHash { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
