using System;
using System.Runtime.InteropServices;
using SayraClient.Kiosk.Application.Interfaces;
using SayraClient.Kiosk.Domain.Events;
using SayraClient.Kiosk.Domain.Models;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Kiosk.Infrastructure.DeviceMonitoring;

public class DeviceControlService : IDeviceControlService
{
    private readonly IAuditLogger _auditLogger;
    private readonly IKioskPolicyService _policyService;
    private bool _isMonitoring;

    public event Action<DeviceConnectedEvent>? DeviceConnected;
    public event Action<DeviceRemovedEvent>? DeviceRemoved;
    public event Action<UnauthorizedDeviceDetectedEvent>? UnauthorizedDeviceDetected;

    private const int WM_DEVICECHANGE = 0x0219;
    private const int DBT_DEVICEARRIVAL = 0x8000;
    private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

    [StructLayout(LayoutKind.Sequential)]
    private struct DEV_BROADCAST_HDR
    {
        public int dbch_size;
        public int dbch_devicetype;
        public int dbch_reserved;
    }

    public DeviceControlService(IAuditLogger auditLogger, IKioskPolicyService policyService)
    {
        _auditLogger = auditLogger;
        _policyService = policyService;
    }

    public void StartMonitoring()
    {
        if (_isMonitoring) return;
        _isMonitoring = true;
        _auditLogger.LogOperational("USB device control monitoring started.");
    }

    public void StopMonitoring()
    {
        if (!_isMonitoring) return;
        _isMonitoring = false;
        _auditLogger.LogOperational("USB device control monitoring stopped.");
    }

    public void HandleDeviceNotification(IntPtr wParam, IntPtr lParam)
    {
        if (!_isMonitoring) return;

        try
        {
            int eventType = wParam.ToInt32();

            if (eventType == DBT_DEVICEARRIVAL)
            {
                _auditLogger.LogOperational("USB device arrival detected via notification.");

                var connectedEvent = new DeviceConnectedEvent
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    DeviceName = "Generic USB Device",
                    Timestamp = DateTime.UtcNow
                };
                DeviceConnected?.Invoke(connectedEvent);

                if (_policyService.IsRestrictionEnabled(RestrictionType.Usb))
                {
                    var unauthorizedEvent = new UnauthorizedDeviceDetectedEvent
                    {
                        DeviceId = connectedEvent.DeviceId,
                        DeviceName = connectedEvent.DeviceName,
                        Timestamp = DateTime.UtcNow
                    };
                    UnauthorizedDeviceDetected?.Invoke(unauthorizedEvent);

                    _auditLogger.LogSecurity($"[Kiosk Security] Unauthorized external storage device detected: {unauthorizedEvent.DeviceName}");
                }
            }
            else if (eventType == DBT_DEVICEREMOVECOMPLETE)
            {
                var removedEvent = new DeviceRemovedEvent
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    DeviceName = "Generic USB Device",
                    Timestamp = DateTime.UtcNow
                };
                DeviceRemoved?.Invoke(removedEvent);

                _auditLogger.LogOperational("USB device removal detected via notification.");
            }
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperational($"Error processing device notification: {ex.Message}");
        }
    }
}
