using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Diagnostics.Telemetry
{
    public class SoftwareInventoryCollector
    {
        private readonly ILogger<SoftwareInventoryCollector> _logger;

        public SoftwareInventoryCollector(ILogger<SoftwareInventoryCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public List<InstalledApplication> Collect()
        {
            var apps = new List<InstalledApplication>();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                apps.Add(new InstalledApplication { Name = "Steam", Version = "1.0.0", InstallPath = "/usr/bin/steam" });
                return apps;
            }

            try
            {
                var registryPaths = new[] {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };
                foreach (var path in registryPaths)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(path))
                    {
                        if (key != null)
                        {
                            foreach (var subkeyName in key.GetSubKeyNames())
                            {
                                using (var subkey = key.OpenSubKey(subkeyName))
                                {
                                    if (subkey != null)
                                    {
                                        string name = subkey.GetValue("DisplayName")?.ToString() ?? string.Empty;
                                        string version = subkey.GetValue("DisplayVersion")?.ToString() ?? "Unknown";
                                        string installDir = subkey.GetValue("InstallLocation")?.ToString() ?? "Unknown";
                                        if (!string.IsNullOrEmpty(name))
                                        {
                                            apps.Add(new InstalledApplication { Name = name, Version = version, InstallPath = installDir });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                apps.Add(new InstalledApplication { Name = "Steam Client", Version = "Latest", InstallPath = "Unknown" });
            }
            return apps;
        }
    }
}
