using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record StorageInformation(
        string SsdHdd,
        long Capacity,
        long FreeSpace,
        string DriveType
    );
}
