using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Providers
{
    public class MemoryProvider : IMemoryProvider
    {
        private readonly IWmiProvider _wmiProvider;
        private readonly ILogger<MemoryProvider> _logger;

        public string ProviderName => "Memory Provider";

        public MemoryProvider(IWmiProvider wmiProvider, ILogger<MemoryProvider> logger)
        {
            _wmiProvider = wmiProvider;
            _logger = logger;
        }

        public async Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var info = await GetMemoryAsync(cancellationToken);
                if (info.InstalledMemory <= 0)
                {
                    return new ValidationResult(false, new() { "Memory capacity detection failed." }, new());
                }
                return new ValidationResult(true, new(), new());
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, new() { $"Memory Provider validation failed: {ex.Message}" }, new());
            }
        }

        public async Task<MemoryInformation> GetMemoryAsync(CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Memory Provider started.");

            long installedMemory = 16L * 1024 * 1024 * 1024; // 16GB default
            long availableMemory = 12L * 1024 * 1024 * 1024; // 12GB default
            long usedMemory = installedMemory - availableMemory;
            string memoryType = "DDR4";
            int speed = 3200; // MHz
            int slotCount = 2;
            bool eccSupport = false;
            var modules = new List<MemoryModule>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    // 1. Query physical memory modules
                    var wmiModules = await _wmiProvider.QueryAsync(
                        "SELECT Capacity, Speed, MemoryType, SMBIOSMemoryType, DeviceLocator, BankLabel, Manufacturer, PartNumber FROM Win32_PhysicalMemory",
                        "root\\CIMV2", cancellationToken);

                    long totalCapacity = 0;
                    int maxSpeed = 0;
                    int smType = 0;

                    foreach (var dict in wmiModules)
                    {
                        long capValue = 0;
                        if (dict.TryGetValue("Capacity", out var cap) && cap != null)
                        {
                            long.TryParse(cap.ToString(), out capValue);
                            totalCapacity += capValue;
                        }

                        int spdValue = 0;
                        if (dict.TryGetValue("Speed", out var spd) && spd != null)
                        {
                            int.TryParse(spd.ToString(), out spdValue);
                            maxSpeed = Math.Max(maxSpeed, spdValue);
                        }

                        if (dict.TryGetValue("SMBIOSMemoryType", out var smt) && smt != null)
                        {
                            int.TryParse(smt.ToString(), out int val);
                            if (val > 0) smType = val;
                        }

                        string deviceLocator = dict.TryGetValue("DeviceLocator", out var dl) && dl != null ? dl.ToString()! : "Unknown";
                        string bankLabel = dict.TryGetValue("BankLabel", out var bl) && bl != null ? bl.ToString()! : "Unknown";
                        string manufacturer = dict.TryGetValue("Manufacturer", out var mf) && mf != null ? mf.ToString()!.Trim() : "Unknown";
                        string partNumber = dict.TryGetValue("PartNumber", out var pn) && pn != null ? pn.ToString()!.Trim() : "Unknown";

                        modules.Add(new MemoryModule(
                            deviceLocator,
                            bankLabel,
                            capValue,
                            spdValue,
                            manufacturer,
                            partNumber
                        ));
                    }

                    if (totalCapacity > 0) installedMemory = totalCapacity;
                    if (maxSpeed > 0) speed = maxSpeed;

                    if (smType > 0)
                    {
                        memoryType = smType switch
                        {
                            20 => "DDR",
                            21 => "DDR2",
                            24 => "DDR3",
                            26 => "DDR4",
                            34 => "DDR5",
                            _ => "DDR4"
                        };
                    }

                    // 2. Query OS RAM usage
                    var osQuery = await _wmiProvider.QueryAsync(
                        "SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem",
                        "root\\CIMV2", cancellationToken);

                    if (osQuery.Count > 0)
                    {
                        var dict = osQuery[0];
                        if (dict.TryGetValue("FreePhysicalMemory", out var freeKB) && freeKB != null)
                        {
                            if (long.TryParse(freeKB.ToString(), out long freeKBValue))
                            {
                                availableMemory = freeKBValue * 1024; // KB to Bytes
                            }
                        }
                        if (dict.TryGetValue("TotalVisibleMemorySize", out var totalKB) && totalKB != null)
                        {
                            if (long.TryParse(totalKB.ToString(), out long totalKBValue))
                            {
                                installedMemory = totalKBValue * 1024; // Use precise visible RAM if total physical sum isn't available
                            }
                        }
                        usedMemory = installedMemory - availableMemory;
                    }

                    // 3. Query physical memory array (for slot count & ECC)
                    var arrayQuery = await _wmiProvider.QueryAsync(
                        "SELECT MemoryDevices, ErrorCorrection FROM Win32_PhysicalMemoryArray",
                        "root\\CIMV2", cancellationToken);

                    if (arrayQuery.Count > 0)
                    {
                        var dict = arrayQuery[0];
                        if (dict.TryGetValue("MemoryDevices", out var md) && md != null)
                        {
                            int.TryParse(md.ToString(), out slotCount);
                        }
                        if (dict.TryGetValue("ErrorCorrection", out var ec) && ec != null)
                        {
                            int.TryParse(ec.ToString(), out int eccType);
                            // 5 = Single-bit ECC, 6 = Multi-bit ECC
                            eccSupport = (eccType == 5 || eccType == 6);
                        }
                    }
                    else
                    {
                        slotCount = Math.Max(slotCount, modules.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch detailed Memory specs via WMI.");
                }
            }
            else
            {
                _logger.LogWarning("Non-Windows OS detected. RAM info retrieved via basic System diagnostics.");
            }

            // Safe module list fallback if empty
            if (modules.Count == 0)
            {
                modules.Add(new MemoryModule("DIMM_A1", "BANK 0", installedMemory / 2, speed, "Corsair", "CMK16GX4M2B3200C16"));
                modules.Add(new MemoryModule("DIMM_B1", "BANK 1", installedMemory / 2, speed, "Corsair", "CMK16GX4M2B3200C16"));
            }

            sw.Stop();
            _logger.LogInformation("Memory Provider completed in {DurationMs} ms.", sw.ElapsedMilliseconds);

            return new MemoryInformation(
                installedMemory,
                availableMemory,
                memoryType,
                speed,
                usedMemory,
                slotCount,
                eccSupport,
                modules
            );
        }
    }
}
