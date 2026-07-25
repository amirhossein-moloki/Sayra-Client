namespace Sayra.Client.Shared.Security.GameProtection.Domain.Models;

public class ProcessInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string? Hash { get; set; }
    public string? Publisher { get; set; }
}
