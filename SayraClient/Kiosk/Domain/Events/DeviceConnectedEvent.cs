using System;

namespace SayraClient.Kiosk.Domain.Events;

public class DeviceConnectedEvent
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
