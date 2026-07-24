namespace Sayra.Client.Configuration.Models;

public class ConfigurationDelta
{
    public string Section { get; set; } = string.Empty;
    public string OldHash { get; set; } = string.Empty;
    public string NewHash { get; set; } = string.Empty;
    public string Patch { get; set; } = string.Empty; // Holds the patch data or the section's new payload
}
