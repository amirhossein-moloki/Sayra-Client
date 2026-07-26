using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using SayraClient.Kiosk.Application.Interfaces;
using SayraClient.Kiosk.Domain.Models;

namespace SayraClient.Kiosk.Infrastructure.DeviceMonitoring
{
    /// <summary>
    /// Implements USB Device Protection.
    ///
    /// Windows Limitations around Hardware-Level Blocking:
    /// 1. True kernel-level hardware USB port blocking requires custom Ring-0 kernel drivers (e.g., filter drivers), which are out of scope.
    /// 2. User-mode USB protection (such as this service) monitors WM_DEVICECHANGE arrival notifications, queries removable storage volumes,
    ///    and programmatically locks, dismounts, and ejects them using Win32 DeviceIoControl APIs.
    /// 3. While highly effective, user-mode ejection depends on the drive being mounted; trusted devices are safely allowed without interruption.
    /// </summary>
    public class WindowsUsbProtectionService : IUsbProtectionService
    {
        private readonly ILogger<WindowsUsbProtectionService> _logger;
        private readonly IAuditLogger _auditLogger;
        private readonly IKioskPolicyService _policyService;

        // In-memory policy list of trusted USB device keywords
        private readonly HashSet<string> _trustedDevices = new(StringComparer.OrdinalIgnoreCase)
        {
            "TrustedKeyboard",
            "TrustedMouse",
            "AdminRecoveryDrive",
            "SafeUSB",
            "SAYRA_Authorized"
        };

        // Native P/Invoke Declarations for Dismount/Eject
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        private const uint FSCTL_LOCK_VOLUME = 0x00090014;
        private const uint FSCTL_DISMOUNT_VOLUME = 0x00090018;
        private const uint IOCTL_STORAGE_EJECT_MEDIA = 0x002D4808;

        public WindowsUsbProtectionService(
            ILogger<WindowsUsbProtectionService> logger,
            IAuditLogger auditLogger,
            IKioskPolicyService policyService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _policyService = policyService ?? throw new ArgumentNullException(nameof(policyService));
        }

        public void HandleDeviceArrival(string deviceId, string deviceName)
        {
            _logger.LogInformation("USB Insert event detected. DeviceName: '{DeviceName}', DeviceId: '{DeviceId}'", deviceName, deviceId);

            // 1. Device Identification
            bool isTrusted = EvaluateTrust(deviceId, deviceName);

            // 2. Policy Evaluation
            bool usbRestrictionEnabled = _policyService.IsRestrictionEnabled(RestrictionType.Usb);

            if (isTrusted)
            {
                _auditLogger.LogOperational($"[USB Protection] Trusted device connected: '{deviceName}'. Access allowed.");
                _logger.LogInformation("USB Protection: Trusted device connection. No action taken.");
                return;
            }

            if (usbRestrictionEnabled)
            {
                // 3. Eject / Dismount Unauthorized Device
                _auditLogger.LogSecurity($"[Kiosk Security] Unauthorized USB device insertion detected: '{deviceName}' ({deviceId}). Initiating defensive unmount.");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    EjectUnauthorizedDrives();
                }
                else
                {
                    _logger.LogWarning("Dismount and Eject operations skipped on non-Windows platform.");
                }
            }
            else
            {
                // Restriction is disabled, just audit the connection
                _auditLogger.LogOperational($"[USB Protection] Unauthorized USB device '{deviceName}' connected, but policy restriction is currently disabled.");
            }
        }

        public void HandleDeviceRemoval(string deviceId, string deviceName)
        {
            _logger.LogInformation("USB Device removed. Name: '{DeviceName}' ID: '{DeviceId}'", deviceName, deviceId);
            _auditLogger.LogOperational($"[USB Protection] USB device disconnected: '{deviceName}' ({deviceId})");
        }

        private bool EvaluateTrust(string deviceId, string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return false;

            foreach (var trusted in _trustedDevices)
            {
                if (deviceName.Contains(trusted, StringComparison.OrdinalIgnoreCase) ||
                    deviceId.Contains(trusted, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void EjectUnauthorizedDrives()
        {
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    if (drive.DriveType == DriveType.Removable && drive.IsReady)
                    {
                        // Check if volume label is trusted
                        if (_trustedDevices.Contains(drive.VolumeLabel))
                        {
                            _logger.LogInformation("Removable volume '{Label}' is trusted. Skipping ejection.", drive.VolumeLabel);
                            continue;
                        }

                        string driveLetter = drive.Name.TrimEnd('\\');
                        _logger.LogInformation("Removable drive {Drive} is unauthorized. Initiating eject/unmount.", driveLetter);

                        EjectVolume(driveLetter);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to scan and eject unauthorized drives.");
            }
        }

        private void EjectVolume(string driveLetter)
        {
            string volumePath = $@"\\.\{driveLetter}";
            IntPtr hDevice = CreateFile(
                volumePath,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hDevice == (IntPtr)(-1) || hDevice == IntPtr.Zero)
            {
                _logger.LogWarning("Failed to open volume handle for {DriveLetter}. Cannot eject.", driveLetter);
                return;
            }

            try
            {
                uint bytesReturned;
                // 1. Lock Volume
                DeviceIoControl(hDevice, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);

                // 2. Dismount Volume
                if (DeviceIoControl(hDevice, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero))
                {
                    _logger.LogInformation("Successfully dismounted volume: '{Drive}'", driveLetter);
                }

                // 3. Eject volume/media
                if (DeviceIoControl(hDevice, IOCTL_STORAGE_EJECT_MEDIA, IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero))
                {
                    _logger.LogInformation("Successfully ejected volume media: '{Drive}'", driveLetter);
                    _auditLogger.LogSecurity($"[Kiosk Security] Unauthorized USB volume {driveLetter} successfully dismounted and ejected.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred during volume eject for drive: '{Drive}'", driveLetter);
            }
            finally
            {
                CloseHandle(hDevice);
            }
        }
    }
}
