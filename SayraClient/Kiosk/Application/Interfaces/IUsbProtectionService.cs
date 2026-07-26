using System;

namespace SayraClient.Kiosk.Application.Interfaces
{
    public interface IUsbProtectionService
    {
        /// <summary>
        /// Handles the arrival/connection of a new USB device and applies security policies.
        /// If unauthorized, unmounts/ejects the volume where possible.
        /// </summary>
        void HandleDeviceArrival(string deviceId, string deviceName);

        /// <summary>
        /// Handles the removal of a USB device.
        /// </summary>
        void HandleDeviceRemoval(string deviceId, string deviceName);
    }
}
