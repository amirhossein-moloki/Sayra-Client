using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record MemoryModule
    {
        public string DeviceLocator { get; init; }
        public string BankLabel { get; init; }
        public long Capacity { get; init; }
        public int Speed { get; init; }
        public string Manufacturer { get; init; }
        public string PartNumber { get; init; }

        public MemoryModule(
            string DeviceLocator,
            string BankLabel,
            long Capacity,
            int Speed,
            string Manufacturer,
            string PartNumber)
        {
            this.DeviceLocator = DeviceLocator;
            this.BankLabel = BankLabel;
            this.Capacity = Capacity;
            this.Speed = Speed;
            this.Manufacturer = Manufacturer;
            this.PartNumber = PartNumber;
        }
    }
}
