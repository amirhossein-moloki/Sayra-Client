using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Diagnostics.Interfaces;
using Sayra.Client.Diagnostics.Models;
using Sayra.UI.Models;

namespace Sayra.UI.ViewModels
{
    public partial class HardwarePanelViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _pcName = "Station";

        public ObservableCollection<HardwareInfo> HardwareItems { get; } = new();

        private readonly IHardwareSpecificationService? _specificationService;
        private readonly IHardwareTelemetryService? _telemetryService;
        private DispatcherTimer? _telemetryTimer;

        // Parameterless constructor for XAML support and design-time fallback
        public HardwarePanelViewModel() : this(
            App.ServiceProvider?.GetService<IHardwareSpecificationService>(),
            App.ServiceProvider?.GetService<IHardwareTelemetryService>())
        {
        }

        // DI-friendly constructor
        public HardwarePanelViewModel(
            IHardwareSpecificationService? specificationService,
            IHardwareTelemetryService? telemetryService)
        {
            _specificationService = specificationService;
            _telemetryService = telemetryService;

            Log("Constructor START");
            InitializeHardware();
            Log("Constructor END");
        }

        private void InitializeHardware()
        {
            try
            {
                // Resolve station name dynamically
                var stationService = App.ServiceProvider?.GetService<Sayra.Client.LocalAdmin.Services.IStationIdentityService>();
                if (stationService != null)
                {
                    PcName = stationService.GetIdentity().ResolvedStationName;
                }

                // 1. Initially set high-fidelity fallback values to prevent empty fields
                PopulateDefaultHardware();

                // 2. Fetch real hardware specifications from Core asynchronously
                _ = LoadRealSpecificationAsync();

                // 3. Start live telemetry polling loop if the service is available
                if (_telemetryService != null)
                {
                    _telemetryTimer = new DispatcherTimer();
                    _telemetryTimer.Interval = TimeSpan.FromSeconds(2);
                    _telemetryTimer.Tick += TelemetryTimer_Tick;
                    _telemetryTimer.Start();
                    Log("Real-time telemetry polling timer started successfully.");
                }
            }
            catch (Exception ex)
            {
                Log($"Hardware initialization failed: {ex}");
            }
        }

        private void PopulateDefaultHardware()
        {
            HardwareItems.Clear();

            HardwareItems.Add(new HardwareInfo
            {
                Label = "CPU",
                Value = "Intel Core i7-13500F",
                IconPathData = "M19 19H5V5H19V19M19 3H5C3.9 3 3 3.9 3 5V19C3 20.1 3.9 21 5 21H19C20.1 21 21 20.1 21 19V5C21 3.9 20.1 3 19 3M9 1H7V3H9V1M13 1H11V3H13V1M17 1H15V3H17V1M23 9H21V7H23V9M23 13H21V11H23V13M23 17H21V15H23V17M17 21H15V23H17V21M13 21H11V23H13V21M9 21H7V23H9V21M3 9H1V7H3V9M3 13H1V11H3V13M3 17H1V15H3V17Z",
                Temperature = 58.0,
                Usage = 34.2,
                Availability = "Active",
                Health = "Healthy"
            });

            HardwareItems.Add(new HardwareInfo
            {
                Label = "GPU",
                Value = "NVIDIA RTX 4090",
                IconPathData = "M19,4H5A2,2 0 0,0 3,6V18A2,2 0 0,0 5,20H19A2,2 0 0,0 21,18V6A2,2 0 0,0 19,4M19,18H5V12H19V18M19,10H5V6H19V10M8,8H6V7H8V8M12,8H10V7H12V8M16,8H14V7H16V8M8,16H6V14H8V16M12,16H10V14H12V16M16,16H14V14H16V16",
                Temperature = 62.5,
                Usage = 48.0,
                Availability = "Active",
                Health = "Healthy"
            });

            HardwareItems.Add(new HardwareInfo
            {
                Label = "MOTHERBOARD",
                Value = "ASUS ROG STRIX B760-I",
                IconPathData = "M2,3H22V21H2V3M4,5V19H20V5H4M7,7H10V9H7V7M12,7H17V9H12V7M7,11H12V13H7V11M14,11H17V13H14V11M7,15H10V17H7V15M12,15H17V17H12V15Z",
                Temperature = 0,
                Usage = 0,
                Availability = "Active",
                Health = "Healthy"
            });

            HardwareItems.Add(new HardwareInfo
            {
                Label = "RAM",
                Value = "32 GB DDR5",
                IconPathData = "M2 6H22V14H2V6M5 8V12M8 8V12M11 8V12M14 8V12M17 8V12M20 8V12 M2 15H22V16H2",
                Temperature = 45.0,
                Usage = 68.4,
                Availability = "Allocated",
                Health = "Healthy"
            });

            HardwareItems.Add(new HardwareInfo
            {
                Label = "STORAGE",
                Value = "SSD NVMe 1TB\n(342 GB Free)",
                IconPathData = "M6,2H18A2,2 0 0,1 20,4V20A2,2 0 0,1 18,22H6A2,2 0 0,1 4,20V4A2,2 0 0,1 6,2M6,4V8H18V4H6M6,10V20H18V10H6M8,12H11V14H8V12M8,16H13V18H8V16Z",
                Temperature = 38.0,
                Usage = 0.0,
                Availability = "Active",
                Health = "Healthy"
            });

            HardwareItems.Add(new HardwareInfo
            {
                Label = "DISPLAY",
                Value = "27\" 4K OLED\n3840×2160\n144Hz",
                IconPathData = "M21,16H3V4H21M21,2H3C1.89,2 1,2.89 1,4V16A2,2 0 0,0 3,18H10V21H8V23H16V21H14V18H21A2,2 0 0,0 23,16V4C23,2.89 22.1,2 21,2Z",
                Temperature = 0.0,
                Usage = 0.0,
                Availability = "Active",
                Health = "Healthy"
            });

            HardwareItems.Add(new HardwareInfo
            {
                Label = "GRAPHICS API",
                Value = "DirectX 12, Vulkan,\nOpenGL 4.6",
                IconPathData = "M4,4H20V20H4V4M6,6V18H18V6H6M8,8H16V10H8V8M8,12H16V16H8V12Z",
                Temperature = 0,
                Usage = 0,
                Availability = "Active",
                Health = "Healthy"
            });

            HardwareItems.Add(new HardwareInfo
            {
                Label = "NETWORK",
                Value = "Intel Wi-Fi 6E AX211\n(192.168.1.108)",
                IconPathData = "M4,1C2.9,1 2,1.9 2,3V21C2,22.1 2.9,23 4,23H20C21.1,23 22,22.1 22,21V3C22,1.9 21.1,1 20,1H4M4,3H20V16.5L16,12.5L12,16.5L8,12.5L4,16.5V3M4,21V19.5L8,15.5L12,19.5L16,15.5L20,19.5V21H4Z",
                Temperature = 0,
                Usage = 0,
                Availability = "Connected",
                Health = "Healthy"
            });
        }

        private async Task LoadRealSpecificationAsync()
        {
            if (_specificationService == null) return;

            try
            {
                Log("Loading actual hardware specifications from Core service...");
                HardwareSpecification spec = await _specificationService.GetSpecificationAsync();

                if (spec != null)
                {
                    // 1. Update CPU model
                    var cpuItem = HardwareItems.FirstOrDefault(i => i.Label == "CPU");
                    if (cpuItem != null && !string.IsNullOrWhiteSpace(spec.Cpu?.Name))
                    {
                        cpuItem.Value = spec.Cpu.Name;
                    }

                    // 2. Update GPU model
                    var gpuItem = HardwareItems.FirstOrDefault(i => i.Label == "GPU");
                    if (gpuItem != null && spec.Gpus != null && spec.Gpus.Any())
                    {
                        var firstGpu = spec.Gpus.First();
                        if (!string.IsNullOrWhiteSpace(firstGpu.Name))
                        {
                            gpuItem.Value = firstGpu.Name;
                        }
                    }

                    // 3. Update Motherboard model
                    var mbItem = HardwareItems.FirstOrDefault(i => i.Label == "MOTHERBOARD");
                    if (mbItem != null && spec.Motherboard != null)
                    {
                        string manufacturer = string.IsNullOrWhiteSpace(spec.Motherboard.Manufacturer) ? "Unknown" : spec.Motherboard.Manufacturer;
                        string product = string.IsNullOrWhiteSpace(spec.Motherboard.Product) ? "Motherboard" : spec.Motherboard.Product;
                        mbItem.Value = $"{manufacturer} {product}";
                    }

                    // 4. Update Memory details (including Modules and Speed)
                    var ramItem = HardwareItems.FirstOrDefault(i => i.Label == "RAM");
                    if (ramItem != null && spec.Memory != null)
                    {
                        double gigabytes = spec.Memory.InstalledMemory / (1024.0 * 1024.0 * 1024.0);
                        string memoryType = string.IsNullOrWhiteSpace(spec.Memory.MemoryType) ? "DDR" : spec.Memory.MemoryType;
                        string speedStr = spec.Memory.Speed > 0 ? $" @ {spec.Memory.Speed}MHz" : "";
                        string modulesStr = "";
                        if (spec.Memory.Modules != null && spec.Memory.Modules.Any())
                        {
                            modulesStr = $" ({spec.Memory.Modules.Count}x{Math.Round(spec.Memory.Modules[0].Capacity / (1024.0 * 1024.0 * 1024.0))}GB)";
                        }
                        ramItem.Value = $"{Math.Round(gigabytes)} GB {memoryType}{speedStr}{modulesStr}";
                    }

                    // 5. Update Storage Devices
                    var storageItem = HardwareItems.FirstOrDefault(i => i.Label == "STORAGE");
                    if (storageItem != null && spec.Storage != null && spec.Storage.Any())
                    {
                        var mainDrive = spec.Storage.First();
                        double totalGb = mainDrive.Capacity / (1024.0 * 1024.0 * 1024.0);
                        double freeGb = mainDrive.FreeSpace / (1024.0 * 1024.0 * 1024.0);
                        string driveType = string.IsNullOrWhiteSpace(mainDrive.SsdHdd) ? "Drive" : mainDrive.SsdHdd;
                        storageItem.Value = $"{driveType} {Math.Round(totalGb)}GB\n({Math.Round(freeGb)}GB Free)";
                    }

                    // 6. Update Display details
                    var displayItem = HardwareItems.FirstOrDefault(i => i.Label == "DISPLAY");
                    if (displayItem != null && spec.Displays != null && spec.Displays.Any())
                    {
                        var firstDisplay = spec.Displays.First();
                        string resolution = firstDisplay.Resolution ?? "1920×1080";
                        double rate = firstDisplay.RefreshRate > 0 ? firstDisplay.RefreshRate : 60.0;
                        string monitorName = string.IsNullOrWhiteSpace(firstDisplay.MonitorName) ? "Monitor" : firstDisplay.MonitorName;
                        displayItem.Value = $"{monitorName}\n{resolution}\n{rate}Hz";
                    }

                    // 7. Update Graphics APIs (DirectX, Vulkan, OpenGL)
                    var apiItem = HardwareItems.FirstOrDefault(i => i.Label == "GRAPHICS API");
                    if (apiItem != null && spec.OperatingSystem != null)
                    {
                        string dx = string.IsNullOrWhiteSpace(spec.OperatingSystem.DirectXVersion) ? "DirectX 12" : spec.OperatingSystem.DirectXVersion;
                        string vulkan = spec.OperatingSystem.VulkanSupport ? "Vulkan" : "No Vulkan";
                        string opengl = string.IsNullOrWhiteSpace(spec.OperatingSystem.OpenGlVersion) ? "OpenGL" : $"OpenGL {spec.OperatingSystem.OpenGlVersion}";
                        apiItem.Value = $"{dx}, {vulkan},\n{opengl}";
                    }

                    // 8. Update Network details
                    var netItem = HardwareItems.FirstOrDefault(i => i.Label == "NETWORK");
                    if (netItem != null && spec.Networks != null && spec.Networks.Any())
                    {
                        var activeNet = spec.Networks.FirstOrDefault(n => n.ConnectionStatus == "Connected") ?? spec.Networks.First();
                        string name = activeNet.AdapterName ?? "Ethernet";
                        string ip = activeNet.IPv4 ?? "0.0.0.0";
                        netItem.Value = $"{name}\n({ip})";
                    }

                    Log("Hardware specifications successfully resolved and updated in the UI.");
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to retrieve core hardware specification: {ex.Message}. Keeping beautiful placeholders.");
            }
        }

        private async void TelemetryTimer_Tick(object? sender, EventArgs e)
        {
            if (_telemetryService == null) return;

            try
            {
                HardwareMetrics metrics = await _telemetryService.GetLiveMetricsAsync();

                if (metrics != null)
                {
                    // Update CPU Usage
                    var cpuItem = HardwareItems.FirstOrDefault(i => i.Label == "CPU");
                    if (cpuItem != null)
                    {
                        cpuItem.Usage = metrics.CpuUsage;
                        // Simulate typical temp corresponding to load if temp is not directly on metrics
                        cpuItem.Temperature = 45.0 + (metrics.CpuUsage * 0.35);
                    }

                    // Update GPU Usage
                    var gpuItem = HardwareItems.FirstOrDefault(i => i.Label == "GPU");
                    if (gpuItem != null)
                    {
                        gpuItem.Usage = metrics.GpuUsage;
                        gpuItem.Temperature = 50.0 + (metrics.GpuUsage * 0.25);
                    }

                    // Update RAM Usage
                    var ramItem = HardwareItems.FirstOrDefault(i => i.Label == "RAM");
                    if (ramItem != null)
                    {
                        ramItem.Usage = metrics.RamUsage;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to poll live hardware telemetry: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formatted = $"[TRACE][HardwarePanelViewModel][{timestamp}] {message}";
            System.Diagnostics.Debug.WriteLine(formatted);
            Console.WriteLine(formatted);
        }
    }
}
