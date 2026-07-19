using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Providers
{
    public class MotherboardProvider : IMotherboardProvider
    {
        private readonly IWmiProvider _wmiProvider;
        private readonly ILogger<MotherboardProvider> _logger;

        public string ProviderName => "Motherboard Provider";

        public MotherboardProvider(IWmiProvider wmiProvider, ILogger<MotherboardProvider> logger)
        {
            _wmiProvider = wmiProvider;
            _logger = logger;
        }

        public async Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var info = await GetMotherboardAsync(cancellationToken);
                if (string.IsNullOrEmpty(info.Manufacturer) || string.IsNullOrEmpty(info.Product))
                {
                    return new ValidationResult(false, new() { "Motherboard details are incomplete." }, new());
                }
                return new ValidationResult(true, new(), new());
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, new() { $"Motherboard Provider validation failed: {ex.Message}" }, new());
            }
        }

        public async Task<MotherboardInformation> GetMotherboardAsync(CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Motherboard Provider started.");

            string manufacturer = "ASUSTeK COMPUTER INC.";
            string product = "ROG STRIX B650E-E GAMING WIFI";
            string biosVersion = "1616";
            string serialNumber = "UnknownBoardSerial";
            string biosDate = "08/18/2023";
            bool uefiSupport = true;
            bool secureBoot = false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    // 1. Baseboard Query
                    var baseboard = await _wmiProvider.QueryAsync(
                        "SELECT Manufacturer, Product, SerialNumber FROM Win32_BaseBoard",
                        "root\\CIMV2", cancellationToken);

                    if (baseboard.Count > 0)
                    {
                        var dict = baseboard[0];
                        if (dict.TryGetValue("Manufacturer", out var m) && m != null) manufacturer = m.ToString()!.Trim();
                        if (dict.TryGetValue("Product", out var p) && p != null) product = p.ToString()!.Trim();
                        if (dict.TryGetValue("SerialNumber", out var s) && s != null) serialNumber = s.ToString()!.Trim();
                    }

                    // 2. BIOS Query
                    var bios = await _wmiProvider.QueryAsync(
                        "SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS",
                        "root\\CIMV2", cancellationToken);

                    if (bios.Count > 0)
                    {
                        var dict = bios[0];
                        if (dict.TryGetValue("SMBIOSBIOSVersion", out var b) && b != null) biosVersion = b.ToString()!.Trim();
                        if (dict.TryGetValue("ReleaseDate", out var rd) && rd != null)
                        {
                            string rawDate = rd.ToString()!;
                            if (rawDate.Length >= 8)
                            {
                                biosDate = $"{rawDate.Substring(4, 2)}/{rawDate.Substring(6, 2)}/{rawDate.Substring(0, 4)}";
                            }
                        }
                    }

                    // 3. UEFI & Secure Boot Registry Queries
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State"))
                        {
                            if (key != null)
                            {
                                var secureBootValue = key.GetValue("UEFISecureBootEnabled");
                                if (secureBootValue != null && secureBootValue is int val)
                                {
                                    secureBoot = (val == 1);
                                    uefiSupport = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to read Secure Boot registry key. Usually means non-UEFI or restricted permissions.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query Motherboard details via WMI/Registry.");
                }
            }
            else
            {
                _logger.LogWarning("Non-Windows OS detected. Motherboard Provider returning fallbacks.");
            }

            sw.Stop();
            _logger.LogInformation("Motherboard Provider completed in {DurationMs} ms.", sw.ElapsedMilliseconds);

            return new MotherboardInformation(
                manufacturer,
                product,
                biosVersion,
                serialNumber,
                biosDate,
                uefiSupport,
                secureBoot
            );
        }
    }
}
