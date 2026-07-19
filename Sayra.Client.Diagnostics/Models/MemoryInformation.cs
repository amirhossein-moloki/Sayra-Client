using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record MemoryInformation(
        long InstalledMemory,
        long AvailableMemory,
        string MemoryType,
        int Speed
    );
}
