using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record CpuInformation(
        string Name,
        string Vendor,
        string Architecture,
        int LogicalCores,
        int PhysicalCores,
        int Threads,
        double BaseClock
    );
}
