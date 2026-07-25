namespace Sayra.Client.Shared.Security.GameProtection.Domain.Models;

public enum IntegrityStatus
{
    Valid,
    Invalid,
    Unknown
}

public class IntegrityResult
{
    public IntegrityStatus Status { get; set; } = IntegrityStatus.Unknown;
    public string Reason { get; set; } = string.Empty;
}
