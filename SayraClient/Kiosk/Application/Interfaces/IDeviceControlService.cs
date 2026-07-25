using System;
using SayraClient.Kiosk.Domain.Events;

namespace SayraClient.Kiosk.Application.Interfaces;

public interface IDeviceControlService
{
    event Action<DeviceConnectedEvent>? DeviceConnected;
    event Action<DeviceRemovedEvent>? DeviceRemoved;
    event Action<UnauthorizedDeviceDetectedEvent>? UnauthorizedDeviceDetected;

    void StartMonitoring();
    void StopMonitoring();
    void HandleDeviceNotification(IntPtr wParam, IntPtr lParam);
}
