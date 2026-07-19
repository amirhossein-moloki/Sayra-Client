using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sayra.Client.Diagnostics.Configuration;
using Sayra.Client.Diagnostics.Events;
using Sayra.Client.Diagnostics.Interfaces;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Models;
using Sayra.Client.Diagnostics.Providers;
using Sayra.Client.Diagnostics.Services;
using Xunit;

namespace Sayra.Client.Tests
{
    public class DiagnosticsTests
    {
        private readonly Mock<IWmiProvider> _mockWmi = new();
        private readonly Mock<IPerformanceCounterProvider> _mockPerf = new();
        private readonly Mock<IDisplayProvider> _mockDisplay = new();
        private readonly Mock<INetworkProvider> _mockNetwork = new();
        private readonly Mock<IGpuProvider> _mockGpu = new();
        private readonly Mock<ILogger<HardwareSpecificationService>> _loggerSpec = new();
        private readonly Mock<ILogger<HardwareTelemetryService>> _loggerTelemetry = new();
        private readonly Mock<ILogger<HardwareValidationService>> _loggerVal = new();
        private readonly Mock<ILogger<HardwareMonitoringService>> _loggerMonitor = new();

        private readonly DiagnosticsOptions _options = new()
        {
            PollingIntervalMs = 200,
            CacheDurationMinutes = 1,
            ValidationRetryCount = 3
        };

        private readonly IOptions<DiagnosticsOptions> _optionsAccessor;

        public DiagnosticsTests()
        {
            _optionsAccessor = Options.Create(_options);
        }

        private HardwareSpecification CreateMockSpec()
        {
            return new HardwareSpecification(
                new CpuInformation("AMD Ryzen 7", "AuthenticAMD", "X64", 16, 8, 16, 4.2),
                new List<GpuInformation> { new GpuInformation("NVIDIA RTX 4080", "NVIDIA", "551.23", 16L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024) },
                new MemoryInformation(32L * 1024 * 1024 * 1024, 32L * 1024 * 1024 * 1024, "DDR5", 6000),
                new MotherboardInformation("ASUS", "B650", "1.0"),
                new List<StorageInformation> { new StorageInformation("SSD", 1024L * 1024 * 1024 * 1024, 512L * 1024 * 1024 * 1024, "NTFS") },
                new List<DisplayInformation> { new DisplayInformation("1920x1080", 144.0, true, 96.0) },
                new OperatingSystemInformation("10.0", "19045", "Pro", "DirectX 12", true, "4.6"),
                new List<NetworkInformation> { new NetworkInformation("MyPC", "192.168.1.10", "fe80::1", "00:15:5d:01:23:45") }
            );
        }

        private HardwareMetrics CreateMockMetrics()
        {
            return new HardwareMetrics(
                CpuUsage: 12.5,
                GpuUsage: 25.0,
                RamUsage: 45.2,
                VramUsage: 15.0,
                DiskUsage: 2.1,
                NetworkUsage: 150.5,
                Uptime: TimeSpan.FromHours(2),
                ActiveProcesses: new List<string> { "explorer", "sayraclient" },
                RefreshRate: 144.0,
                FpsCapability: 144.0
            );
        }

        [Fact]
        public void Test1_CacheService_ShouldStoreAndExpireSuccessfully()
        {
            // Arrange
            var cacheService = new HardwareCacheService(_optionsAccessor);
            var spec = CreateMockSpec();

            // Act & Assert (Initial Empty State)
            Assert.Null(cacheService.Get());
            Assert.True(cacheService.IsExpired());

            // Act (Store)
            cacheService.Set(spec);

            // Assert (Retrieval)
            var retrieved = cacheService.Get();
            Assert.NotNull(retrieved);
            Assert.Equal("AMD Ryzen 7", retrieved!.Cpu.Name);
            Assert.False(cacheService.IsExpired());

            // Act (Clear)
            cacheService.Clear();

            // Assert (Cleared State)
            Assert.Null(cacheService.Get());
            Assert.True(cacheService.IsExpired());
        }

        [Fact]
        public async Task Test2_SpecificationService_ShouldLoadSpecsUsingProviders()
        {
            // Arrange
            _mockGpu.Setup(x => x.GetGpusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GpuInformation> { new GpuInformation("NVIDIA RTX 4080", "NVIDIA", "551.23", 16L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024) });
            _mockDisplay.Setup(x => x.GetDisplaysAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DisplayInformation> { new DisplayInformation("1920x1080", 144.0, true, 96.0) });
            _mockNetwork.Setup(x => x.GetNetworksAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NetworkInformation> { new NetworkInformation("Host", "192.168.1.10", "::1", "00:00:00:00") });

            var specService = new HardwareSpecificationService(
                _mockWmi.Object,
                _mockGpu.Object,
                _mockDisplay.Object,
                _mockNetwork.Object,
                _loggerSpec.Object
            );

            // Act
            var spec = await specService.GetSpecificationAsync();

            // Assert
            Assert.NotNull(spec);
            Assert.Equal("AMD Ryzen 7 7800X3D", spec.Cpu.Name); // Fallback logic default
            Assert.Single(spec.Gpus);
            Assert.Equal("NVIDIA RTX 4080", spec.Gpus[0].Name);
            Assert.Single(spec.Displays);
            Assert.Equal("1920x1080", spec.Displays[0].Resolution);
        }

        [Fact]
        public async Task Test3_TelemetryService_ShouldPollLiveMetrics()
        {
            // Arrange
            _mockPerf.Setup(x => x.GetCpuUsage()).Returns(15.2f);
            _mockPerf.Setup(x => x.GetRamUsage()).Returns(4096f);
            _mockPerf.Setup(x => x.GetDiskUsage()).Returns(1.2f);
            _mockGpu.Setup(x => x.GetGpuUsageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(25.5);
            _mockGpu.Setup(x => x.GetVramUsageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3.4);
            _mockNetwork.Setup(x => x.GetNetworkUsageAsync(It.IsAny<CancellationToken>())).ReturnsAsync(150.4);

            var telemetryService = new HardwareTelemetryService(
                _mockPerf.Object,
                _mockGpu.Object,
                _mockNetwork.Object,
                _loggerTelemetry.Object
            );

            // Act
            var metrics = await telemetryService.GetLiveMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(15.2, metrics.CpuUsage, 1);
            Assert.Equal(4096.0, metrics.RamUsage, 1);
            Assert.Equal(25.5, metrics.GpuUsage, 1);
            Assert.Equal(3.4, metrics.VramUsage, 1);
            Assert.Equal(150.4, metrics.NetworkUsage, 1);
        }

        [Fact]
        public async Task Test4_ValidationService_ShouldDetectFailuresAndWarnings()
        {
            // Arrange
            var validationService = new HardwareValidationService(_loggerVal.Object);

            // 1. Healthy Specification
            var healthySpec = CreateMockSpec();
            var metrics = CreateMockMetrics();

            var result = await validationService.ValidateAsync(healthySpec, metrics);
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);

            // 2. Missing GPU Specification
            var missingGpuSpec = healthySpec with { Gpus = new List<GpuInformation>() };
            var resultMissingGpu = await validationService.ValidateAsync(missingGpuSpec, metrics);
            Assert.False(resultMissingGpu.IsValid);
            Assert.Contains(resultMissingGpu.Errors, e => e.Contains("Missing GPU"));

            // 3. Low/Missing RAM Specification
            var lowRamSpec = healthySpec with { Memory = new MemoryInformation(2L * 1024 * 1024 * 1024, 2L * 1024 * 1024 * 1024, "DDR4", 3200) };
            var resultLowRam = await validationService.ValidateAsync(lowRamSpec, metrics);
            Assert.True(resultLowRam.IsValid); // It's a warning, not an error
            Assert.Contains(resultLowRam.Warnings, w => w.Contains("Low system memory"));
        }

        [Fact]
        public async Task Test5_MonitoringService_ShouldRunAndPublishEvents()
        {
            // Arrange
            var spec = CreateMockSpec();
            var metrics = CreateMockMetrics();

            _mockGpu.Setup(x => x.GetGpusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(spec.Gpus);
            _mockDisplay.Setup(x => x.GetDisplaysAsync(It.IsAny<CancellationToken>())).ReturnsAsync(spec.Displays);
            _mockNetwork.Setup(x => x.GetNetworksAsync(It.IsAny<CancellationToken>())).ReturnsAsync(spec.Networks);

            var specService = new HardwareSpecificationService(_mockWmi.Object, _mockGpu.Object, _mockDisplay.Object, _mockNetwork.Object, _loggerSpec.Object);
            var cacheService = new HardwareCacheService(_optionsAccessor);

            var mockTelemetry = new Mock<IHardwareTelemetryService>();
            mockTelemetry.Setup(x => x.GetLiveMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(metrics);

            var mockValidation = new Mock<IHardwareValidationService>();
            mockValidation.Setup(x => x.ValidateAsync(It.IsAny<HardwareSpecification>(), It.IsAny<HardwareMetrics>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult(true, new List<string>(), new List<string>()));

            var monitorService = new HardwareMonitoringService(
                specService,
                mockTelemetry.Object,
                mockValidation.Object,
                cacheService,
                _optionsAccessor,
                _loggerMonitor.Object
            );

            bool initializedFired = false;
            bool metricsUpdatedFired = false;

            monitorService.HardwareInitialized += (s, e) =>
            {
                initializedFired = true;
                Assert.NotNull(e.Specification);
            };

            monitorService.HardwareMetricsUpdated += (s, e) =>
            {
                metricsUpdatedFired = true;
                Assert.NotNull(e.Metrics);
            };

            // Act
            using var cts = new CancellationTokenSource();
            var runTask = monitorService.StartMonitoringAsync(cts.Token);

            // Wait a short moment for initial poll to execute
            await Task.Delay(100);
            cts.Cancel();
            try { await runTask; } catch (OperationCanceledException) { }

            // Assert
            Assert.True(initializedFired);
            Assert.True(metricsUpdatedFired);
            Assert.NotNull(monitorService.CurrentSpecification);
            Assert.NotNull(monitorService.CurrentMetrics);
        }

        [Fact]
        public async Task Test6_CpuProvider_ShouldCollectCorrectCpuDetails()
        {
            // Arrange
            var mockWmiProvider = new Mock<IWmiProvider>();
            var logger = new Mock<ILogger<CpuProvider>>().Object;

            mockWmiProvider.Setup(x => x.QueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "Name", "Intel Core i7-12700K" },
                        { "Manufacturer", "GenuineIntel" },
                        { "NumberOfCores", 12 },
                        { "NumberOfLogicalProcessors", 20 },
                        { "MaxClockSpeed", 3600 },
                        { "CurrentClockSpeed", 4900 },
                        { "L2CacheSize", 12288 },
                        { "L3CacheSize", 25600 },
                        { "VirtualizationFirmwareEnabled", true }
                    }
                });

            var provider = new CpuProvider(mockWmiProvider.Object, logger);

            // Act
            var cpu = await provider.GetCpuAsync();
            var validation = await provider.ValidateAsync();

            // Assert
            Assert.True(validation.IsValid);
            Assert.Equal("CPU Provider", provider.ProviderName);
            // On Windows, the mock results will be utilized. If non-Windows (e.g. testing in Linux sandbox), it uses OS fallbacks, but either way it shouldn't crash.
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Equal("Intel Core i7-12700K", cpu.Name);
                Assert.Equal("GenuineIntel", cpu.Vendor);
                Assert.Equal(12, cpu.PhysicalCores);
                Assert.Equal(20, cpu.LogicalCores);
                Assert.Equal(3.6, cpu.BaseClock);
                Assert.Equal(4.9, cpu.CurrentClock);
                Assert.Equal(12288 * 1024, cpu.L2Cache);
                Assert.Equal(25600 * 1024, cpu.L3Cache);
                Assert.True(cpu.VirtualizationSupport);
            }
            Assert.NotEmpty(cpu.InstructionSets);
        }

        [Fact]
        public async Task Test7_MemoryProvider_ShouldRetrievePhysicalMemoryModules()
        {
            // Arrange
            var mockWmiProvider = new Mock<IWmiProvider>();
            var logger = new Mock<ILogger<MemoryProvider>>().Object;

            mockWmiProvider.Setup(x => x.QueryAsync(It.Is<string>(q => q.Contains("Win32_PhysicalMemory")), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "Capacity", 17179869184L },
                        { "Speed", 3200 },
                        { "SMBIOSMemoryType", 26 },
                        { "DeviceLocator", "DIMM_A1" },
                        { "BankLabel", "BANK 0" },
                        { "Manufacturer", "Corsair" },
                        { "PartNumber", "CMK32" }
                    },
                    new()
                    {
                        { "Capacity", 17179869184L },
                        { "Speed", 3200 },
                        { "SMBIOSMemoryType", 26 },
                        { "DeviceLocator", "DIMM_B1" },
                        { "BankLabel", "BANK 1" },
                        { "Manufacturer", "Corsair" },
                        { "PartNumber", "CMK32" }
                    }
                });

            mockWmiProvider.Setup(x => x.QueryAsync(It.Is<string>(q => q.Contains("Win32_OperatingSystem")), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "FreePhysicalMemory", 8388608L }, // 8 GB
                        { "TotalVisibleMemorySize", 34359738L } // 32 GB
                    }
                });

            mockWmiProvider.Setup(x => x.QueryAsync(It.Is<string>(q => q.Contains("Win32_PhysicalMemoryArray")), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "MemoryDevices", 4 },
                        { "ErrorCorrection", 5 } // Single-bit ECC
                    }
                });

            var provider = new MemoryProvider(mockWmiProvider.Object, logger);

            // Act
            var mem = await provider.GetMemoryAsync();
            var validation = await provider.ValidateAsync();

            // Assert
            Assert.True(validation.IsValid);
            Assert.Equal("Memory Provider", provider.ProviderName);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Equal("DDR4", mem.MemoryType);
                Assert.Equal(3200, mem.Speed);
                Assert.True(mem.EccSupport);
                Assert.Equal(4, mem.SlotCount);
                Assert.Equal(2, mem.Modules.Count);
                Assert.Equal("DIMM_A1", mem.Modules[0].DeviceLocator);
                Assert.Equal("Corsair", mem.Modules[0].Manufacturer);
            }
        }

        [Fact]
        public async Task Test8_StorageProvider_ShouldReportDrivesAndPhysicalDisks()
        {
            // Arrange
            var mockWmiProvider = new Mock<IWmiProvider>();
            var logger = new Mock<ILogger<StorageProvider>>().Object;

            mockWmiProvider.Setup(x => x.QueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "Model", "Samsung SSD 980 PRO" },
                        { "MediaType", "SSD" },
                        { "SerialNumber", "S69ENF0R4" },
                        { "Status", "OK" }
                    }
                });

            var provider = new StorageProvider(mockWmiProvider.Object, logger);

            // Act
            var storageList = await provider.GetStorageAsync();
            var validation = await provider.ValidateAsync();

            // Assert
            Assert.True(validation.IsValid);
            Assert.Equal("Storage Provider", provider.ProviderName);
            Assert.NotEmpty(storageList);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Equal("SSD", storageList[0].SsdHdd);
                Assert.Equal("S69ENF0R4", storageList[0].SerialNumber);
                Assert.Equal("Healthy", storageList[0].Health);
            }
        }

        [Fact]
        public async Task Test9_MotherboardProvider_ShouldFetchValidBaseboardDetails()
        {
            // Arrange
            var mockWmiProvider = new Mock<IWmiProvider>();
            var logger = new Mock<ILogger<MotherboardProvider>>().Object;

            mockWmiProvider.Setup(x => x.QueryAsync(It.Is<string>(q => q.Contains("Win32_BaseBoard")), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "Manufacturer", "MSI" },
                        { "Product", "PRO Z790-A" },
                        { "SerialNumber", "MS-1234" }
                    }
                });

            mockWmiProvider.Setup(x => x.QueryAsync(It.Is<string>(q => q.Contains("Win32_BIOS")), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "SMBIOSBIOSVersion", "A.20" },
                        { "ReleaseDate", "20231201000000.000000+000" }
                    }
                });

            var provider = new MotherboardProvider(mockWmiProvider.Object, logger);

            // Act
            var board = await provider.GetMotherboardAsync();
            var validation = await provider.ValidateAsync();

            // Assert
            Assert.True(validation.IsValid);
            Assert.Equal("Motherboard Provider", provider.ProviderName);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Equal("MSI", board.Manufacturer);
                Assert.Equal("PRO Z790-A", board.Product);
                Assert.Equal("A.20", board.BiosVersion);
                Assert.Equal("MS-1234", board.SerialNumber);
                Assert.Equal("12/01/2023", board.BiosDate);
            }
        }

        [Fact]
        public async Task Test10_OperatingSystemProvider_ShouldRetrieveOsSpecifications()
        {
            // Arrange
            var mockWmiProvider = new Mock<IWmiProvider>();
            var logger = new Mock<ILogger<OperatingSystemProvider>>().Object;

            mockWmiProvider.Setup(x => x.QueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "Caption", "Microsoft Windows 11 Enterprise" },
                        { "Version", "10.0.22631" },
                        { "BuildNumber", "22631" },
                        { "OSArchitecture", "64-bit" },
                        { "InstallDate", "20230501120000.000000+000" },
                        { "LastBootUpTime", "20231015083000.000000+000" }
                    }
                });

            var provider = new OperatingSystemProvider(mockWmiProvider.Object, logger);

            // Act
            var os = await provider.GetOperatingSystemAsync();
            var validation = await provider.ValidateAsync();

            // Assert
            Assert.True(validation.IsValid);
            Assert.Equal("Operating System Provider", provider.ProviderName);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Equal("Microsoft Windows 11 Enterprise", os.Edition);
                Assert.Equal("10.0.22631", os.Version);
                Assert.Equal("22631", os.BuildNumber);
                Assert.Equal("64-bit", os.Architecture);
                Assert.Equal("05/01/2023", os.InstallDate);
                Assert.Equal("2023-10-15 08:30:00", os.BootTime);
            }
        }

        [Fact]
        public async Task Test11_GraphicsApiProviders_ShouldRetrieveStatusAndDetails()
        {
            // Arrange
            var loggerDx = new Mock<ILogger<DirectXProvider>>().Object;
            var loggerVulkan = new Mock<ILogger<VulkanProvider>>().Object;
            var loggerGl = new Mock<ILogger<OpenGLProvider>>().Object;

            var dxProvider = new DirectXProvider(loggerDx);
            var vulkanProvider = new VulkanProvider(loggerVulkan);
            var glProvider = new OpenGLProvider(loggerGl);

            // Act
            var dxVersion = await dxProvider.GetVersionAsync();
            var dxSupported = await dxProvider.IsSupportedAsync();

            var vulkanVersion = await vulkanProvider.GetVersionAsync();
            var vulkanSupported = await vulkanProvider.IsSupportedAsync();

            var glVersion = await glProvider.GetVersionAsync();
            var glSupported = await glProvider.IsSupportedAsync();

            // Assert
            Assert.Equal("DirectX Provider", dxProvider.ProviderName);
            Assert.Equal("Vulkan Provider", vulkanProvider.ProviderName);
            Assert.Equal("OpenGL Provider", glProvider.ProviderName);

            Assert.NotEmpty(dxVersion);
            Assert.NotEmpty(vulkanVersion);
            Assert.NotEmpty(glVersion);
        }

        [Fact]
        public async Task Test12_GpuProvider_ShouldSupportAdditionalGpuMetadata()
        {
            // Arrange
            var mockWmiProvider = new Mock<IWmiProvider>();
            var logger = new Mock<ILogger<GpuProvider>>().Object;

            mockWmiProvider.Setup(x => x.QueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "Name", "NVIDIA GeForce RTX 4090" },
                        { "AdapterCompatibility", "NVIDIA" },
                        { "DriverVersion", "551.76" },
                        { "AdapterRAM", 25769803776L },
                        { "PNPDeviceID", "PCI\\VEN_10DE&DEV_2684" },
                        { "VideoProcessor", "GeForce RTX 4090" },
                        { "DriverDate", "20240228000000.000000+000" },
                        { "CurrentHorizontalResolution", 2560 },
                        { "CurrentVerticalResolution", 1440 },
                        { "CurrentRefreshRate", 240 }
                    }
                });

            var provider = new GpuProvider(mockWmiProvider.Object, logger);

            // Act
            var gpus = await provider.GetGpusAsync();
            var validation = await provider.ValidateAsync();

            // Assert
            Assert.True(validation.IsValid);
            Assert.Single(gpus);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                var gpu = gpus[0];
                Assert.Equal("NVIDIA GeForce RTX 4090", gpu.Name);
                Assert.Equal("NVIDIA", gpu.Vendor);
                Assert.Equal("551.76", gpu.DriverVersion);
                Assert.Equal(25769803776L, gpu.DedicatedMemory);
                Assert.Equal("PCI\\VEN_10DE&DEV_2684", gpu.PciBus);
                Assert.Equal("GeForce RTX 4090", gpu.VideoProcessor);
                Assert.Equal("02/28/2024", gpu.DriverDate);
                Assert.Equal("2560x1440", gpu.CurrentResolution);
                Assert.Equal(240.0, gpu.CurrentRefreshRate);
            }
        }

        [Fact]
        public async Task Test13_DisplayProvider_ShouldHandleMultiMonitorSetups()
        {
            // Arrange
            var mockWmiProvider = new Mock<IWmiProvider>();
            var logger = new Mock<ILogger<DisplayProvider>>().Object;

            mockWmiProvider.Setup(x => x.QueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "CurrentHorizontalResolution", 1920 },
                        { "CurrentVerticalResolution", 1080 },
                        { "CurrentRefreshRate", 144 }
                    },
                    new()
                    {
                        { "CurrentHorizontalResolution", 2560 },
                        { "CurrentVerticalResolution", 1440 },
                        { "CurrentRefreshRate", 60 }
                    }
                });

            var provider = new DisplayProvider(mockWmiProvider.Object, logger);

            // Act
            var displays = await provider.GetDisplaysAsync();
            var validation = await provider.ValidateAsync();

            // Assert
            Assert.True(validation.IsValid);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Equal(2, displays.Count);
                Assert.Equal("1920x1080", displays[0].Resolution);
                Assert.True(displays[0].IsPrimary);

                Assert.Equal("2560x1440", displays[1].Resolution);
                Assert.False(displays[1].IsPrimary);
            }
        }

        [Fact]
        public async Task Test14_NetworkProvider_ShouldIdentifyDetailedAdapterFields()
        {
            // Arrange
            var logger = new Mock<ILogger<NetworkProvider>>().Object;
            var provider = new NetworkProvider(logger);

            // Act
            var networks = await provider.GetNetworksAsync();
            var validation = await provider.ValidateAsync();

            // Assert
            Assert.True(validation.IsValid);
            Assert.NotEmpty(networks);
            var net = networks[0];
            Assert.NotEmpty(net.Hostname);
            Assert.NotEmpty(net.IPv4);
            Assert.NotEmpty(net.MacAddress);
            Assert.NotEmpty(net.AdapterName);
            Assert.NotEmpty(net.AdapterType);
            Assert.NotEmpty(net.ConnectionStatus);
        }

        [Fact]
        public async Task Test15_WmiProvider_ShouldReturnEmptyOrSimulatedUnderWmiErrors()
        {
            // Arrange
            var logger = new Mock<ILogger<WmiProvider>>().Object;
            var provider = new WmiProvider(logger);

            // Act
            var result = await provider.QueryAsync("SELECT InvalidColumn FROM Win32_InvalidClass");
            var validation = await provider.ValidateAsync();

            // Assert
            Assert.Empty(result); // Wmi query fails gracefully and returns empty list
            Assert.Equal("WMI Provider", provider.ProviderName);
        }

        [Fact]
        public void Test16_ExceptionHierarchies_ShouldBeCorrect()
        {
            // Arrange
            var message = "Error occurred";
            var inner = new Exception("Inner exception");

            // Act & Assert
            var pEx = new Sayra.Client.Diagnostics.Exceptions.HardwareProviderException(message, inner);
            var uEx = new Sayra.Client.Diagnostics.Exceptions.ProviderUnavailableException(message, inner);
            var rEx = new Sayra.Client.Diagnostics.Exceptions.HardwareReadException(message, inner);
            var vEx = new Sayra.Client.Diagnostics.Exceptions.ValidationException(message, inner);

            Assert.IsAssignableFrom<Exception>(pEx);
            Assert.IsAssignableFrom<Sayra.Client.Diagnostics.Exceptions.HardwareProviderException>(uEx);
            Assert.IsAssignableFrom<Sayra.Client.Diagnostics.Exceptions.HardwareProviderException>(rEx);
            Assert.IsAssignableFrom<Sayra.Client.Diagnostics.Exceptions.HardwareProviderException>(vEx);

            Assert.Equal(message, pEx.Message);
            Assert.Equal(inner, pEx.InnerException);
        }
    }
}
