using System;
using System.Collections.Generic;

namespace Sayra.Client.Diagnostics.Models
{
    public record HardwareSpecification(
        CpuInformation Cpu,
        List<GpuInformation> Gpus,
        MemoryInformation Memory,
        MotherboardInformation Motherboard,
        List<StorageInformation> Storage,
        List<DisplayInformation> Displays,
        OperatingSystemInformation OperatingSystem,
        List<NetworkInformation> Networks
    );
}
