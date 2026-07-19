using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record GpuInformation(
        string Name,
        string Vendor,
        string DriverVersion,
        long DedicatedMemory,
        long SharedMemory
    );
}
