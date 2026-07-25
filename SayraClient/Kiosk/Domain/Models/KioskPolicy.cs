using System.Collections.Generic;

namespace SayraClient.Kiosk.Domain.Models;

public class KioskPolicy
{
    public bool EnableKeyboardRestriction { get; set; } = true;
    public bool EnableMouseRestriction { get; set; } = true;
    public bool EnableSystemRestriction { get; set; } = true;
    public bool EnableUsbRestriction { get; set; } = true;
    public bool MaintenanceModeAllowed { get; set; } = true;
    public List<SecurityRestriction> Restrictions { get; set; } = new();
}
