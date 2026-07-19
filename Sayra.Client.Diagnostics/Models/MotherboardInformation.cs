using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record MotherboardInformation
    {
        public string Manufacturer { get; init; }
        public string Product { get; init; }
        public string BiosVersion { get; init; }
        public string SerialNumber { get; init; }
        public string BiosDate { get; init; }
        public bool UefiSupport { get; init; }
        public bool SecureBoot { get; init; }

        public MotherboardInformation(
            string manufacturer,
            string product,
            string biosVersion,
            string serialNumber = "Unknown",
            string biosDate = "Unknown",
            bool uefiSupport = false,
            bool secureBoot = false)
        {
            Manufacturer = manufacturer;
            Product = product;
            BiosVersion = biosVersion;
            SerialNumber = serialNumber;
            BiosDate = biosDate;
            UefiSupport = uefiSupport;
            SecureBoot = secureBoot;
        }
    }
}
