using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Services
{
    public class HardwareValidationService : IHardwareValidationService
    {
        private readonly ILogger<HardwareValidationService> _logger;

        public HardwareValidationService(ILogger<HardwareValidationService> logger)
        {
            _logger = logger;
        }

        public Task<ValidationResult> ValidateAsync(HardwareSpecification spec, HardwareMetrics metrics, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Executing hardware specification and metrics validation...");

            var errors = new List<string>();
            var warnings = new List<string>();

            // 1. GPU Validation
            if (spec.Gpus == null || spec.Gpus.Count == 0)
            {
                errors.Add("Missing GPU: No graphics processing adapters detected on the system.");
            }
            else
            {
                foreach (var gpu in spec.Gpus)
                {
                    if (string.IsNullOrWhiteSpace(gpu.DriverVersion) || gpu.DriverVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        warnings.Add($"Unknown GPU Driver: The graphics adapter '{gpu.Name}' is using an unknown or generic driver.");
                    }
                }
            }

            // 2. RAM Validation
            if (spec.Memory == null || spec.Memory.InstalledMemory <= 0)
            {
                errors.Add("Missing RAM: No physical system memory was detected.");
            }
            else if (spec.Memory.InstalledMemory < 4L * 1024 * 1024 * 1024) // < 4 GB
            {
                warnings.Add($"Low system memory: Only {spec.Memory.InstalledMemory / (1024 * 1024 * 1024)} GB RAM is installed, which may degrade client performance.");
            }

            // 3. Display Validation
            if (spec.Displays == null || spec.Displays.Count == 0)
            {
                errors.Add("Invalid Display: No active monitor display detected.");
            }
            else
            {
                foreach (var display in spec.Displays)
                {
                    if (string.IsNullOrWhiteSpace(display.Resolution) || display.Resolution.Equals("0x0"))
                    {
                        errors.Add("Invalid Display: Primary display horizontal or vertical resolution is unreadable.");
                    }
                    if (display.RefreshRate <= 0)
                    {
                        warnings.Add($"Low refresh rate: Display is configured at {display.RefreshRate} Hz.");
                    }
                }
            }

            // 4. Network Validation
            if (spec.Networks == null || spec.Networks.Count == 0)
            {
                errors.Add("Offline Network: No active network adapters found on this device.");
            }
            else
            {
                bool activeIpFound = false;
                foreach (var net in spec.Networks)
                {
                    if (!string.IsNullOrWhiteSpace(net.IPv4) && !net.IPv4.Equals("127.0.0.1") && !net.IPv4.Equals("0.0.0.0"))
                    {
                        activeIpFound = true;
                    }
                }

                if (!activeIpFound)
                {
                    warnings.Add("Offline Network: Device has no valid external IP address configured (using loopback or none).");
                }
            }

            // 5. WMI Health / Validation Fallback (e.g. bios/motherboard validation)
            if (spec.Motherboard == null || string.IsNullOrWhiteSpace(spec.Motherboard.Manufacturer))
            {
                warnings.Add("Invalid WMI: Motherboard query returned unreadable details, fallback metrics active.");
            }

            bool isValid = errors.Count == 0;
            _logger.LogInformation("Hardware validation completed. Status: {Status} ({ErrorCount} errors, {WarningCount} warnings)",
                isValid ? "VALID" : "INVALID", errors.Count, warnings.Count);

            return Task.FromResult(new ValidationResult(isValid, errors, warnings));
        }
    }
}
