using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sayra.Client.Shared.Models.Phase9.Domain
{
    /// <summary>
    /// Represents a detailed hardware asset configuration.
    /// </summary>
    public record HardwareAsset
    {
        /// <summary>
        /// Gets the unique identifier of the hardware asset.
        /// </summary>
        [JsonPropertyName("hardwareId")]
        public string HardwareId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the associated machine identifier.
        /// </summary>
        [JsonPropertyName("machineId")]
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the manufacturer of the hardware asset.
        /// </summary>
        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; init; } = string.Empty;

        /// <summary>
        /// Gets the model of the hardware asset.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        /// <summary>
        /// Gets the unique serial number of the hardware.
        /// </summary>
        [JsonPropertyName("serialNumber")]
        public string SerialNumber { get; init; } = string.Empty;

        /// <summary>
        /// Gets the part number or identifier.
        /// </summary>
        [JsonPropertyName("partNumber")]
        public string PartNumber { get; init; } = string.Empty;

        /// <summary>
        /// Gets the asset tag applied by the enterprise.
        /// </summary>
        [JsonPropertyName("assetTag")]
        public string AssetTag { get; init; } = string.Empty;

        /// <summary>
        /// Gets the associated warranty information.
        /// </summary>
        [JsonPropertyName("warranty")]
        public AssetWarranty Warranty { get; init; } = new();

        /// <summary>
        /// Gets the associated asset lifecycle information.
        /// </summary>
        [JsonPropertyName("lifecycle")]
        public AssetLifecycle Lifecycle { get; init; } = new();

        /// <summary>
        /// Gets custom specifications for the hardware.
        /// </summary>
        [JsonPropertyName("specifications")]
        public Dictionary<string, string> Specifications { get; init; } = new();

        /// <summary>
        /// Validates the structure and properties of the hardware asset.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(HardwareId) &&
                   !string.IsNullOrWhiteSpace(MachineId) &&
                   !string.IsNullOrWhiteSpace(SerialNumber);
        }

        /// <summary>
        /// Overrides record equality to ensure deep dictionary comparison of specifications.
        /// </summary>
        public virtual bool Equals(HardwareAsset? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            bool specsEqual = Specifications.Count == other.Specifications.Count;
            if (specsEqual)
            {
                foreach (var kvp in Specifications)
                {
                    if (!other.Specifications.TryGetValue(kvp.Key, out var val) || val != kvp.Value)
                    {
                        specsEqual = false;
                        break;
                    }
                }
            }

            return HardwareId == other.HardwareId &&
                   MachineId == other.MachineId &&
                   Manufacturer == other.Manufacturer &&
                   Model == other.Model &&
                   SerialNumber == other.SerialNumber &&
                   PartNumber == other.PartNumber &&
                   AssetTag == other.AssetTag &&
                   Warranty == other.Warranty &&
                   Lifecycle == other.Lifecycle &&
                   specsEqual;
        }

        /// <summary>
        /// Overrides HashCode for correct dictionary comparisons.
        /// </summary>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(HardwareId);
            hash.Add(MachineId);
            hash.Add(Manufacturer);
            hash.Add(Model);
            hash.Add(SerialNumber);
            hash.Add(PartNumber);
            hash.Add(AssetTag);
            hash.Add(Warranty);
            hash.Add(Lifecycle);
            foreach (var kvp in Specifications)
            {
                hash.Add(kvp.Key);
                hash.Add(kvp.Value);
            }
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Represents an installed software application asset.
    /// </summary>
    public record SoftwareAsset
    {
        /// <summary>
        /// Gets the unique identifier for the software asset.
        /// </summary>
        [JsonPropertyName("softwareId")]
        public string SoftwareId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the associated machine identifier.
        /// </summary>
        [JsonPropertyName("machineId")]
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the name of the software application.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the installed software version.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; init; } = string.Empty;

        /// <summary>
        /// Gets the publisher or developer of the software.
        /// </summary>
        [JsonPropertyName("publisher")]
        public string Publisher { get; init; } = string.Empty;

        /// <summary>
        /// Gets the installation date.
        /// </summary>
        [JsonPropertyName("installDate")]
        public DateTime? InstallDate { get; init; }

        /// <summary>
        /// Gets the folder path where the software is installed.
        /// </summary>
        [JsonPropertyName("installPath")]
        public string InstallPath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the total size in bytes occupied by the installation.
        /// </summary>
        [JsonPropertyName("sizeBytes")]
        public long SizeBytes { get; init; }

        /// <summary>
        /// Validates the structure and properties of the software asset.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(SoftwareId) &&
                   !string.IsNullOrWhiteSpace(MachineId) &&
                   !string.IsNullOrWhiteSpace(Name) &&
                   !string.IsNullOrWhiteSpace(Version);
        }
    }

    /// <summary>
    /// Represents a software license entitlement.
    /// </summary>
    public record LicenseAsset
    {
        /// <summary>
        /// Gets the unique license identifier.
        /// </summary>
        [JsonPropertyName("licenseId")]
        public string LicenseId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the license key or authorization certificate.
        /// </summary>
        [JsonPropertyName("licenseKey")]
        public string LicenseKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets the associated product identifier.
        /// </summary>
        [JsonPropertyName("productId")]
        public string ProductId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the targeted software name.
        /// </summary>
        [JsonPropertyName("softwareName")]
        public string SoftwareName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the licensing model or type (e.g., PerSeat, Floating, Trial).
        /// </summary>
        [JsonPropertyName("licenseType")]
        public string LicenseType { get; init; } = string.Empty;

        /// <summary>
        /// Gets the total seat capacity for floating/multi-device licenses.
        /// </summary>
        [JsonPropertyName("seatCapacity")]
        public int SeatCapacity { get; init; }

        /// <summary>
        /// Gets the expiration date of the license.
        /// </summary>
        [JsonPropertyName("expiryDate")]
        public DateTime? ExpiryDate { get; init; }

        /// <summary>
        /// Validates the structure of the license asset.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(LicenseId) &&
                   !string.IsNullOrWhiteSpace(SoftwareName);
        }
    }

    /// <summary>
    /// Represents a system device driver asset.
    /// </summary>
    public record DriverAsset
    {
        /// <summary>
        /// Gets the unique driver identifier.
        /// </summary>
        [JsonPropertyName("driverId")]
        public string DriverId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the device name the driver controls.
        /// </summary>
        [JsonPropertyName("deviceName")]
        public string DeviceName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the driver provider or manufacturer.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; init; } = string.Empty;

        /// <summary>
        /// Gets the driver version.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; init; } = string.Empty;

        /// <summary>
        /// Gets the release date of the driver.
        /// </summary>
        [JsonPropertyName("releaseDate")]
        public DateTime? ReleaseDate { get; init; }

        /// <summary>
        /// Gets the driver INF file name.
        /// </summary>
        [JsonPropertyName("infName")]
        public string InfName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the underlying hardware ID of the device.
        /// </summary>
        [JsonPropertyName("hardwareId")]
        public string HardwareId { get; init; } = string.Empty;

        /// <summary>
        /// Validates the structure of the driver asset.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(DriverId) &&
                   !string.IsNullOrWhiteSpace(DeviceName) &&
                   !string.IsNullOrWhiteSpace(Version);
        }
    }

    /// <summary>
    /// Represents a device firmware asset.
    /// </summary>
    public record FirmwareAsset
    {
        /// <summary>
        /// Gets the unique firmware identifier.
        /// </summary>
        [JsonPropertyName("firmwareId")]
        public string FirmwareId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the device or component name.
        /// </summary>
        [JsonPropertyName("componentName")]
        public string ComponentName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the firmware version.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; init; } = string.Empty;

        /// <summary>
        /// Gets the firmware release date.
        /// </summary>
        [JsonPropertyName("releaseDate")]
        public DateTime? ReleaseDate { get; init; }

        /// <summary>
        /// Gets the device manufacturer.
        /// </summary>
        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; init; } = string.Empty;

        /// <summary>
        /// Validates the structure of the firmware asset.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(FirmwareId) &&
                   !string.IsNullOrWhiteSpace(ComponentName) &&
                   !string.IsNullOrWhiteSpace(Version);
        }
    }

    /// <summary>
    /// Represents motherboard BIOS details.
    /// </summary>
    public record BIOSAsset
    {
        /// <summary>
        /// Gets the unique BIOS identifier.
        /// </summary>
        [JsonPropertyName("biosId")]
        public string BiosId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the BIOS manufacturer.
        /// </summary>
        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; init; } = string.Empty;

        /// <summary>
        /// Gets the BIOS version.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; init; } = string.Empty;

        /// <summary>
        /// Gets the BIOS release date.
        /// </summary>
        [JsonPropertyName("releaseDate")]
        public DateTime? ReleaseDate { get; init; }

        /// <summary>
        /// Gets the motherboard system serial number.
        /// </summary>
        [JsonPropertyName("serialNumber")]
        public string SerialNumber { get; init; } = string.Empty;

        /// <summary>
        /// Validates the structure of the BIOS asset.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(BiosId) &&
                   !string.IsNullOrWhiteSpace(Version);
        }
    }

    /// <summary>
    /// Represents a storage drive hardware asset.
    /// </summary>
    public record StorageAsset
    {
        /// <summary>
        /// Gets the unique storage identifier.
        /// </summary>
        [JsonPropertyName("storageId")]
        public string StorageId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the local device name (e.g., C:, /dev/sda).
        /// </summary>
        [JsonPropertyName("deviceName")]
        public string DeviceName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the disk drive model.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        /// <summary>
        /// Gets the physical serial number of the drive.
        /// </summary>
        [JsonPropertyName("serialNumber")]
        public string SerialNumber { get; init; } = string.Empty;

        /// <summary>
        /// Gets the storage size limit in bytes.
        /// </summary>
        [JsonPropertyName("sizeBytes")]
        public long SizeBytes { get; init; }

        /// <summary>
        /// Gets the interface type (e.g., NVMe, SATA).
        /// </summary>
        [JsonPropertyName("interfaceType")]
        public string InterfaceType { get; init; } = string.Empty;

        /// <summary>
        /// Gets the media type (e.g., SSD, HDD).
        /// </summary>
        [JsonPropertyName("mediaType")]
        public string MediaType { get; init; } = string.Empty;

        /// <summary>
        /// Gets the remaining health/wear level percentage.
        /// </summary>
        [JsonPropertyName("healthPercentage")]
        public double HealthPercentage { get; init; } = 100.0;

        /// <summary>
        /// Validates the structure of the storage asset.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(StorageId) &&
                   !string.IsNullOrWhiteSpace(DeviceName);
        }
    }

    /// <summary>
    /// Represents Graphics Processing Unit details.
    /// </summary>
    public record GPUAsset
    {
        /// <summary>
        /// Gets the unique GPU identifier.
        /// </summary>
        [JsonPropertyName("gpuId")]
        public string GpuId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the marketing name of the GPU (e.g., NVIDIA GeForce RTX 4090).
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the core chipset identifier.
        /// </summary>
        [JsonPropertyName("chipset")]
        public string Chipset { get; init; } = string.Empty;

        /// <summary>
        /// Gets the active GPU driver version.
        /// </summary>
        [JsonPropertyName("driverVersion")]
        public string DriverVersion { get; init; } = string.Empty;

        /// <summary>
        /// Gets the onboard Video RAM capacity in bytes.
        /// </summary>
        [JsonPropertyName("vramBytes")]
        public long VramBytes { get; init; }

        /// <summary>
        /// Gets the GPU board manufacturer.
        /// </summary>
        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; init; } = string.Empty;

        /// <summary>
        /// Validates the structure of the GPU asset.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(GpuId) &&
                   !string.IsNullOrWhiteSpace(Name);
        }
    }

    /// <summary>
    /// Represents Central Processing Unit details.
    /// </summary>
    public record CPUAsset
    {
        /// <summary>
        /// Gets the unique CPU identifier.
        /// </summary>
        [JsonPropertyName("cpuId")]
        public string CpuId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the CPU processor model name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the physical cores count.
        /// </summary>
        [JsonPropertyName("cores")]
        public int Cores { get; init; }

        /// <summary>
        /// Gets the logical threads count.
        /// </summary>
        [JsonPropertyName("threads")]
        public int Threads { get; init; }

        /// <summary>
        /// Gets the base clock frequency in Hz.
        /// </summary>
        [JsonPropertyName("baseClockHz")]
        public double BaseClockHz { get; init; }

        /// <summary>
        /// Gets the maximum boost frequency in Hz.
        /// </summary>
        [JsonPropertyName("maxClockHz")]
        public double MaxClockHz { get; init; }

        /// <summary>
        /// Gets the processor architecture (e.g., x64, ARM64).
        /// </summary>
        [JsonPropertyName("architecture")]
        public string Architecture { get; init; } = string.Empty;

        /// <summary>
        /// Gets the CPU manufacturer (e.g., Intel, AMD).
        /// </summary>
        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; init; } = string.Empty;

        /// <summary>
        /// Validates the structure of the CPU asset.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(CpuId) &&
                   !string.IsNullOrWhiteSpace(Name);
        }
    }

    /// <summary>
    /// Represents system RAM memory modules specifications.
    /// </summary>
    public record MemoryAsset
    {
        /// <summary>
        /// Gets the unique memory module identifier.
        /// </summary>
        [JsonPropertyName("memoryId")]
        public string MemoryId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the module storage capacity in bytes.
        /// </summary>
        [JsonPropertyName("capacityBytes")]
        public long CapacityBytes { get; init; }

        /// <summary>
        /// Gets the operating clock speed in MHz.
        /// </summary>
        [JsonPropertyName("speedMhz")]
        public int SpeedMhz { get; init; }

        /// <summary>
        /// Gets the hardware form factor (e.g., DIMM, SO-DIMM).
        /// </summary>
        [JsonPropertyName("formFactor")]
        public string FormFactor { get; init; } = string.Empty;

        /// <summary>
        /// Gets the manufacturer part number.
        /// </summary>
        [JsonPropertyName("partNumber")]
        public string PartNumber { get; init; } = string.Empty;

        /// <summary>
        /// Gets the chip manufacturer.
        /// </summary>
        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; init; } = string.Empty;

        /// <summary>
        /// Validates the structure of the memory asset.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(MemoryId) &&
                   CapacityBytes > 0;
        }
    }

    /// <summary>
    /// Represents a network device/adapter asset.
    /// </summary>
    public record NetworkAsset
    {
        /// <summary>
        /// Gets the unique network identifier.
        /// </summary>
        [JsonPropertyName("networkId")]
        public string NetworkId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the name of the network adapter.
        /// </summary>
        [JsonPropertyName("adapterName")]
        public string AdapterName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the adapter physical MAC address.
        /// </summary>
        [JsonPropertyName("macAddress")]
        public string MacAddress { get; init; } = string.Empty;

        /// <summary>
        /// Gets the currently assigned IP address.
        /// </summary>
        [JsonPropertyName("ipAddress")]
        public string IpAddress { get; init; } = string.Empty;

        /// <summary>
        /// Gets the maximum link speed in bits per second.
        /// </summary>
        [JsonPropertyName("speedBitsPerSec")]
        public long SpeedBitsPerSec { get; init; }

        /// <summary>
        /// Gets whether DHCP configuration is active.
        /// </summary>
        [JsonPropertyName("isDhcpEnabled")]
        public bool IsDhcpEnabled { get; init; }

        /// <summary>
        /// Validates the structure of the network asset.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(NetworkId) &&
                   !string.IsNullOrWhiteSpace(AdapterName);
        }
    }

    /// <summary>
    /// Tracks historical records of changes or events for an asset.
    /// </summary>
    public record AssetHistory
    {
        /// <summary>
        /// Gets the unique tracking identifier for the history log.
        /// </summary>
        [JsonPropertyName("historyId")]
        public string HistoryId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the targeted asset identifier.
        /// </summary>
        [JsonPropertyName("assetId")]
        public string AssetId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the workstation machine identifier containing the asset.
        /// </summary>
        [JsonPropertyName("machineId")]
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the recorded timestamp of the event.
        /// </summary>
        [JsonPropertyName("timestampUtc")]
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the historical event action/type (e.g., Added, Discovered, Modified, Removed).
        /// </summary>
        [JsonPropertyName("eventType")]
        public string EventType { get; init; } = string.Empty;

        /// <summary>
        /// Gets the detailed description text of the event.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets the administrator operator identifier who initiated the change (if any).
        /// </summary>
        [JsonPropertyName("operatorId")]
        public string OperatorId { get; init; } = string.Empty;

        /// <summary>
        /// Validates the structure of the asset history record.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(HistoryId) &&
                   !string.IsNullOrWhiteSpace(AssetId) &&
                   !string.IsNullOrWhiteSpace(MachineId) &&
                   !string.IsNullOrWhiteSpace(EventType);
        }
    }

    /// <summary>
    /// Tracks a specific property value change on an asset.
    /// </summary>
    public record AssetChangeRecord
    {
        /// <summary>
        /// Gets the unique identifier for the change tracking record.
        /// </summary>
        [JsonPropertyName("changeId")]
        public string ChangeId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the associated asset identifier.
        /// </summary>
        [JsonPropertyName("assetId")]
        public string AssetId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the hosting machine identifier.
        /// </summary>
        [JsonPropertyName("machineId")]
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the recorded timestamp of the change.
        /// </summary>
        [JsonPropertyName("timestampUtc")]
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the classification of the change (e.g., Modified, VersionChange).
        /// </summary>
        [JsonPropertyName("changeType")]
        public string ChangeType { get; init; } = string.Empty;

        /// <summary>
        /// Gets the name of the modified property.
        /// </summary>
        [JsonPropertyName("propertyName")]
        public string PropertyName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the old property value before modification.
        /// </summary>
        [JsonPropertyName("oldValue")]
        public string OldValue { get; init; } = string.Empty;

        /// <summary>
        /// Gets the new property value after modification.
        /// </summary>
        [JsonPropertyName("newValue")]
        public string NewValue { get; init; } = string.Empty;

        /// <summary>
        /// Validates the structure of the asset change record.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(ChangeId) &&
                   !string.IsNullOrWhiteSpace(AssetId) &&
                   !string.IsNullOrWhiteSpace(MachineId) &&
                   !string.IsNullOrWhiteSpace(PropertyName);
        }
    }

    /// <summary>
    /// Represents the warranty information for a hardware asset.
    /// </summary>
    public record AssetWarranty
    {
        /// <summary>
        /// Gets the name of the warranty provider.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; init; } = string.Empty;

        /// <summary>
        /// Gets the warranty start date.
        /// </summary>
        [JsonPropertyName("startDate")]
        public DateTime? StartDate { get; init; }

        /// <summary>
        /// Gets the warranty coverage end date.
        /// </summary>
        [JsonPropertyName("endDate")]
        public DateTime? EndDate { get; init; }

        /// <summary>
        /// Gets the current status of the warranty coverage (e.g., Active, Expired).
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        /// <summary>
        /// Gets the covered tier or support level description.
        /// </summary>
        [JsonPropertyName("supportLevel")]
        public string SupportLevel { get; init; } = string.Empty;
    }

    /// <summary>
    /// Tracks asset purchase, installation, and disposal lifecycle events.
    /// </summary>
    public record AssetLifecycle
    {
        /// <summary>
        /// Gets the purchase date of the asset.
        /// </summary>
        [JsonPropertyName("purchaseDate")]
        public DateTime? PurchaseDate { get; init; }

        /// <summary>
        /// Gets the installation timestamp.
        /// </summary>
        [JsonPropertyName("installDate")]
        public DateTime? InstallDate { get; init; }

        /// <summary>
        /// Gets the planned End of Life (EOL) date.
        /// </summary>
        [JsonPropertyName("endOfLifeDate")]
        public DateTime? EndOfLifeDate { get; init; }

        /// <summary>
        /// Gets the current depreciated financial value of the asset.
        /// </summary>
        [JsonPropertyName("depreciatedValue")]
        public decimal DepreciatedValue { get; init; }

        /// <summary>
        /// Gets the actual disposal timestamp of the asset.
        /// </summary>
        [JsonPropertyName("disposalDate")]
        public DateTime? DisposalDate { get; init; }

        /// <summary>
        /// Gets the chosen disposal method.
        /// </summary>
        [JsonPropertyName("disposalMethod")]
        public string DisposalMethod { get; init; } = string.Empty;
    }
}
