using System;

namespace SayraClient.Kiosk.Domain.Events;

public class UnauthorizedDeviceDetectedEvent
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = "USB Mass Storage";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
