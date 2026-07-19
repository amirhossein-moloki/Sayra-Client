using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record HardwareHealth(
        bool IsCpuHealthy,
        bool IsGpuHealthy,
        bool IsRamHealthy,
        bool IsStorageHealthy,
        bool IsDisplayHealthy,
        bool IsNetworkHealthy,
        bool IsWmiHealthy,
        string Details
    );
}
