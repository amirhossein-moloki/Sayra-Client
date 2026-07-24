using System;

namespace Sayra.Client.Configuration.Models;

public class ConfigurationPackage
{
    public long Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public string IssuedBy { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string PayloadType { get; set; } = string.Empty; // "Full" or "Delta"
    public string TargetClient { get; set; } = string.Empty;
    public string TargetGroup { get; set; } = string.Empty;
}
