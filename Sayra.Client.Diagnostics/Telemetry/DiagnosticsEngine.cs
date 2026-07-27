using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Diagnostics.Telemetry
{
    public class DiagnosticsEngine : IDiagnosticsEngine
    {
        private readonly ICpuProvider _cpuProvider;
        private readonly IGpuProvider _gpuProvider;
        private readonly IMemoryProvider _memoryProvider;
        private readonly IStorageProvider _storageProvider;
        private readonly INetworkProvider _networkProvider;
        private readonly SoftwareInventoryCollector _softwareCollector;
        private readonly ProcessInventoryCollector _processCollector;
        private readonly DriverInventoryCollector _driverCollector;
        private readonly ILogger<DiagnosticsEngine> _logger;

        public DiagnosticsEngine(
            ICpuProvider cpuProvider,
            IGpuProvider gpuProvider,
            IMemoryProvider memoryProvider,
            IStorageProvider storageProvider,
            INetworkProvider networkProvider,
            SoftwareInventoryCollector softwareCollector,
            ProcessInventoryCollector processCollector,
            DriverInventoryCollector driverCollector,
            ILogger<DiagnosticsEngine> logger)
        {
            _cpuProvider = cpuProvider ?? throw new ArgumentNullException(nameof(cpuProvider));
            _gpuProvider = gpuProvider ?? throw new ArgumentNullException(nameof(gpuProvider));
            _memoryProvider = memoryProvider ?? throw new ArgumentNullException(nameof(memoryProvider));
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
            _networkProvider = networkProvider ?? throw new ArgumentNullException(nameof(networkProvider));
            _softwareCollector = softwareCollector ?? throw new ArgumentNullException(nameof(softwareCollector));
            _processCollector = processCollector ?? throw new ArgumentNullException(nameof(processCollector));
            _driverCollector = driverCollector ?? throw new ArgumentNullException(nameof(driverCollector));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SystemDiagnosticsReport> GenerateFullReportAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Generating full diagnostics report...");
            var report = new SystemDiagnosticsReport { Timestamp = DateTime.UtcNow, MachineId = Environment.MachineName };

            try
            {
                var cpuInfo = await _cpuProvider.GetCpuAsync(cancellationToken);
                report.Cpu = new CpuDiagInfo { Name = cpuInfo.Name, Vendor = cpuInfo.Vendor, LogicalCores = cpuInfo.LogicalCores, PhysicalCores = cpuInfo.PhysicalCores, BaseClockGhz = cpuInfo.BaseClock };
            } catch { }

            try
            {
                var gpuInfos = await _gpuProvider.GetGpusAsync(cancellationToken);
                report.Gpus = gpuInfos.Select(g => new GpuDiagInfo { Name = g.Name, Vendor = g.Vendor, DriverVersion = g.DriverVersion, DedicatedVramBytes = g.DedicatedMemory }).ToList();
            } catch { }

            try
            {
                var memInfo = await _memoryProvider.GetMemoryAsync(cancellationToken);
                report.Memory = new MemoryDiagInfo { TotalBytes = memInfo.InstalledMemory, AvailableBytes = memInfo.AvailableMemory, MemoryType = memInfo.MemoryType, SpeedMhz = memInfo.Speed };
            } catch { }

            try
            {
                var storageInfos = await _storageProvider.GetStorageAsync(cancellationToken);
                report.Storage = storageInfos.Select(s => new StorageDiagInfo { DriveLetter = s.DriveLetter, VolumeLabel = s.VolumeLabel, CapacityBytes = s.Capacity, FreeSizeBytes = s.FreeSpace, HealthStatus = s.Health, DriveType = s.SsdHdd, SerialNumber = s.SerialNumber }).ToList();
            } catch { }

            try
            {
                var networkInfos = await _networkProvider.GetNetworksAsync(cancellationToken);
                report.Networks = networkInfos.Select(n => new NetworkDiagInfo { AdapterName = n.AdapterName, Ipv4Address = n.IPv4, MacAddress = n.MacAddress, Status = n.ConnectionStatus, SpeedBps = n.LinkSpeed }).ToList();
            } catch { }

            try { report.SoftwareInventory = _softwareCollector.Collect(); } catch { }
            try { report.ProcessInventory = _processCollector.Collect(cancellationToken); } catch { }
            try { report.DriverInventory = await _driverCollector.CollectAsync(cancellationToken); } catch { }

            return report;
        }
    }
}
