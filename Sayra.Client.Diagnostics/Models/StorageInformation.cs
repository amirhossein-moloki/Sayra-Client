using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record StorageInformation
    {
        public string SsdHdd { get; init; }
        public long Capacity { get; init; }
        public long FreeSpace { get; init; }
        public string DriveType { get; init; }
        public string DriveLetter { get; init; }
        public string VolumeLabel { get; init; }
        public string Filesystem { get; init; }
        public long UsedSpace { get; init; }
        public string Health { get; init; }
        public string SerialNumber { get; init; }

        public StorageInformation(
            string SsdHdd,
            long Capacity,
            long FreeSpace,
            string DriveType,
            string DriveLetter = "Unknown",
            string VolumeLabel = "Unknown",
            string Filesystem = "Unknown",
            long UsedSpace = 0,
            string Health = "Healthy",
            string SerialNumber = "Unknown")
        {
            this.SsdHdd = SsdHdd;
            this.Capacity = Capacity;
            this.FreeSpace = FreeSpace;
            this.DriveType = DriveType;
            this.DriveLetter = DriveLetter;
            this.VolumeLabel = VolumeLabel;
            this.Filesystem = Filesystem;
            this.UsedSpace = UsedSpace;
            this.Health = Health;
            this.SerialNumber = SerialNumber;
        }
    }
}
