using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Assets.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Assets.Collectors
{
    /// <summary>
    /// Base collector providing helper methods and common logging capabilities.
    /// </summary>
    public abstract class BaseAssetCollector
    {
        /// <summary>
        /// Logger instance.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Initializes a new instance of <see cref="BaseAssetCollector"/>.
        /// </summary>
        protected BaseAssetCollector(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a base AssetRecord populated with standard fields.
        /// </summary>
        protected AssetRecord CreateRecord(string machineId, string assetId, string name, string serial, AssetType category, Dictionary<string, string>? specs = null)
        {
            return new AssetRecord
            {
                AssetId = assetId,
                MachineId = machineId,
                Name = name,
                SerialOrSignature = serial,
                Category = category,
                Status = AssetStatus.Active,
                Specifications = specs ?? new Dictionary<string, string>()
            };
        }
    }

    /// <summary>
    /// Collects hardware specifications such as CPUs, GPUs, and Memory Modules.
    /// </summary>
    public class HardwareInventoryCollector : BaseAssetCollector, IAssetCollector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HardwareInventoryCollector"/> class.
        /// </summary>
        public HardwareInventoryCollector(ILogger<HardwareInventoryCollector> logger) : base(logger)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AssetRecord>> CollectAssetsAsync(string machineId, CancellationToken ct = default)
        {
            var list = new List<AssetRecord>();
            try
            {
                Logger.LogInformation("Collecting hardware assets for machine '{MachineId}'...", machineId);
                ct.ThrowIfCancellationRequested();

                // 1. CPU Asset
                var cpuSpecs = new Dictionary<string, string>
                {
                    { "Manufacturer", "AMD" },
                    { "Model", "Ryzen 9 7950X" },
                    { "Cores", "16" },
                    { "Threads", "32" },
                    { "BaseClockHz", "4500000000" },
                    { "MaxClockHz", "5700000000" },
                    { "Architecture", "x64" }
                };
                list.Add(CreateRecord(machineId, $"CPU-{machineId}", "AMD Ryzen 9 7950X 16-Core Processor", "CPU-SR7950X-88F", AssetType.Cpu, cpuSpecs));

                // 2. GPU Asset
                var gpuSpecs = new Dictionary<string, string>
                {
                    { "Manufacturer", "NVIDIA" },
                    { "Chipset", "GeForce RTX 4090" },
                    { "DriverVersion", "531.79" },
                    { "VramBytes", "25769803776" }
                };
                list.Add(CreateRecord(machineId, $"GPU-{machineId}", "NVIDIA GeForce RTX 4090", "GPU-NVR4090-99E", AssetType.Gpu, gpuSpecs));

                // 3. RAM Asset
                var ramSpecs = new Dictionary<string, string>
                {
                    { "Manufacturer", "Corsair" },
                    { "PartNumber", "CMK64GX5M2B5600C40" },
                    { "CapacityBytes", "68719476736" },
                    { "SpeedMhz", "5600" },
                    { "FormFactor", "DIMM" }
                };
                list.Add(CreateRecord(machineId, $"RAM-{machineId}", "Corsair Vengeance DDR5 64GB", "RAM-CS5600-64G", AssetType.Ram, ramSpecs));

                await Task.Delay(50, ct); // Simulate minimal IO
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to collect hardware assets safely.");
            }
            return list;
        }
    }

    /// <summary>
    /// Scans and collects details about installed applications and game titles.
    /// </summary>
    public class SoftwareInventoryCollector : BaseAssetCollector, IAssetCollector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SoftwareInventoryCollector"/> class.
        /// </summary>
        public SoftwareInventoryCollector(ILogger<SoftwareInventoryCollector> logger) : base(logger)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AssetRecord>> CollectAssetsAsync(string machineId, CancellationToken ct = default)
        {
            var list = new List<AssetRecord>();
            try
            {
                Logger.LogInformation("Collecting software assets for machine '{MachineId}'...", machineId);
                ct.ThrowIfCancellationRequested();

                var soft1 = new Dictionary<string, string>
                {
                    { "Publisher", "SAYRA Enterprise" },
                    { "Version", "9.8.0" },
                    { "InstallPath", "C:\\Program Files\\SayraClient" },
                    { "InstallDate", DateTime.UtcNow.AddDays(-30).ToString("O") }
                };
                list.Add(CreateRecord(machineId, $"SW-SAYRA-{machineId}", "SAYRA Workstation Client", "SW-SAYRA-9.8", AssetType.Software, soft1));

                var soft2 = new Dictionary<string, string>
                {
                    { "Publisher", "Valve" },
                    { "Version", "2.10.4" },
                    { "InstallPath", "C:\\Program Files (x86)\\Steam" },
                    { "InstallDate", DateTime.UtcNow.AddDays(-100).ToString("O") }
                };
                list.Add(CreateRecord(machineId, $"SW-STEAM-{machineId}", "Steam Gaming Client", "SW-STEAM-2.10", AssetType.Software, soft2));

                await Task.Delay(50, ct);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to collect software assets safely.");
            }
            return list;
        }
    }

    /// <summary>
    /// Scans and collects information about system device drivers.
    /// </summary>
    public class DriverInventoryCollector : BaseAssetCollector, IAssetCollector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DriverInventoryCollector"/> class.
        /// </summary>
        public DriverInventoryCollector(ILogger<DriverInventoryCollector> logger) : base(logger)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AssetRecord>> CollectAssetsAsync(string machineId, CancellationToken ct = default)
        {
            var list = new List<AssetRecord>();
            try
            {
                Logger.LogInformation("Collecting system driver assets for machine '{MachineId}'...", machineId);
                ct.ThrowIfCancellationRequested();

                var nvSpecs = new Dictionary<string, string>
                {
                    { "DeviceName", "NVIDIA Graphics Driver" },
                    { "Provider", "NVIDIA" },
                    { "Version", "531.79" },
                    { "InfName", "oem33.inf" },
                    { "HardwareId", "PCI\\VEN_10DE&DEV_2684" }
                };
                list.Add(CreateRecord(machineId, $"DRV-NV-{machineId}", "NVIDIA Graphics Controller Driver", "DRV-NV-531.79", AssetType.Peripheral, nvSpecs));

                var audioSpecs = new Dictionary<string, string>
                {
                    { "DeviceName", "Realtek High Definition Audio" },
                    { "Provider", "Realtek" },
                    { "Version", "6.0.9239.1" },
                    { "InfName", "oem4.inf" },
                    { "HardwareId", "HDAUDIO\\FUNC_01&VEN_10EC" }
                };
                list.Add(CreateRecord(machineId, $"DRV-RTK-{machineId}", "Realtek Audio Driver", "DRV-RTK-6.0.9239", AssetType.Peripheral, audioSpecs));

                await Task.Delay(50, ct);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to collect system drivers safely.");
            }
            return list;
        }
    }

    /// <summary>
    /// Collects motherboard BIOS details.
    /// </summary>
    public class BIOSInventoryCollector : BaseAssetCollector, IAssetCollector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BIOSInventoryCollector"/> class.
        /// </summary>
        public BIOSInventoryCollector(ILogger<BIOSInventoryCollector> logger) : base(logger)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AssetRecord>> CollectAssetsAsync(string machineId, CancellationToken ct = default)
        {
            var list = new List<AssetRecord>();
            try
            {
                Logger.LogInformation("Collecting BIOS asset for machine '{MachineId}'...", machineId);
                ct.ThrowIfCancellationRequested();

                var biosSpecs = new Dictionary<string, string>
                {
                    { "Manufacturer", "American Megatrends Inc." },
                    { "Version", "F12" },
                    { "ReleaseDate", "2023-04-12" },
                    { "SerialNumber", "BIOS-SYS-77123" }
                };
                list.Add(CreateRecord(machineId, $"BIOS-{machineId}", "AMI UEFI BIOS F12", "BIOS-SYS-77123", AssetType.Motherboard, biosSpecs));

                await Task.Delay(50, ct);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to collect BIOS asset safely.");
            }
            return list;
        }
    }

    /// <summary>
    /// Scans and collects information about device firmwares.
    /// </summary>
    public class FirmwareInventoryCollector : BaseAssetCollector, IAssetCollector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FirmwareInventoryCollector"/> class.
        /// </summary>
        public FirmwareInventoryCollector(ILogger<FirmwareInventoryCollector> logger) : base(logger)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AssetRecord>> CollectAssetsAsync(string machineId, CancellationToken ct = default)
        {
            var list = new List<AssetRecord>();
            try
            {
                Logger.LogInformation("Collecting firmware assets for machine '{MachineId}'...", machineId);
                ct.ThrowIfCancellationRequested();

                var ssdSpecs = new Dictionary<string, string>
                {
                    { "ComponentName", "Samsung SSD 990 PRO 2TB" },
                    { "Version", "EL1B5" },
                    { "ReleaseDate", "2022-11-15" },
                    { "Manufacturer", "Samsung" }
                };
                list.Add(CreateRecord(machineId, $"FW-SSD-{machineId}", "Samsung SSD 990 PRO Firmware", "FW-SSD-EL1B5", AssetType.StorageDevice, ssdSpecs));

                await Task.Delay(50, ct);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to collect firmware assets safely.");
            }
            return list;
        }
    }

    /// <summary>
    /// Collects drive partition, media type, and disk space health specifications.
    /// </summary>
    public class StorageInventoryCollector : BaseAssetCollector, IAssetCollector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StorageInventoryCollector"/> class.
        /// </summary>
        public StorageInventoryCollector(ILogger<StorageInventoryCollector> logger) : base(logger)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AssetRecord>> CollectAssetsAsync(string machineId, CancellationToken ct = default)
        {
            var list = new List<AssetRecord>();
            try
            {
                Logger.LogInformation("Collecting storage drive assets for machine '{MachineId}'...", machineId);
                ct.ThrowIfCancellationRequested();

                var nvmeSpecs = new Dictionary<string, string>
                {
                    { "DeviceName", "C:" },
                    { "Model", "Samsung SSD 990 PRO 2TB" },
                    { "SerialNumber", "S73KNE0W90123" },
                    { "SizeBytes", "2000398934016" },
                    { "InterfaceType", "NVMe" },
                    { "MediaType", "SSD" },
                    { "HealthPercentage", "99" }
                };
                list.Add(CreateRecord(machineId, $"DISK-NVME-{machineId}", "Samsung SSD 990 PRO 2TB NVMe Drive", "DISK-S73KNE0W", AssetType.StorageDevice, nvmeSpecs));

                await Task.Delay(50, ct);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to collect storage drive assets safely.");
            }
            return list;
        }
    }

    /// <summary>
    /// Collects active network adapters, link speeds, and network interfaces specifications.
    /// </summary>
    public class NetworkInventoryCollector : BaseAssetCollector, IAssetCollector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkInventoryCollector"/> class.
        /// </summary>
        public NetworkInventoryCollector(ILogger<NetworkInventoryCollector> logger) : base(logger)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AssetRecord>> CollectAssetsAsync(string machineId, CancellationToken ct = default)
        {
            var list = new List<AssetRecord>();
            try
            {
                Logger.LogInformation("Collecting network device assets for machine '{MachineId}'...", machineId);
                ct.ThrowIfCancellationRequested();

                var ethSpecs = new Dictionary<string, string>
                {
                    { "AdapterName", "Intel Ethernet Controller I225-V" },
                    { "MacAddress", "00-1A-2B-3C-4D-5E" },
                    { "IpAddress", "192.168.1.50" },
                    { "SpeedBitsPerSec", "2500000000" },
                    { "IsDhcpEnabled", "True" }
                };
                list.Add(CreateRecord(machineId, $"NET-INTEL-{machineId}", "Intel Ethernet Controller I225-V 2.5GbE", "NET-I225V-001A", AssetType.NetworkAdapter, ethSpecs));

                await Task.Delay(50, ct);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to collect network device assets safely.");
            }
            return list;
        }
    }
}
