namespace SayraClient.Kiosk.Domain.Models;

public class SecurityRestriction
{
    public RestrictionType Type { get; set; }
    public PolicyState State { get; set; }
    public string Name { get; set; } = string.Empty;
}
