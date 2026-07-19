using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Providers
{
    public class DisplayProvider : IDisplayProvider
    {
        private readonly IWmiProvider _wmiProvider;
        private readonly ILogger<DisplayProvider> _logger;

        public DisplayProvider(IWmiProvider wmiProvider, ILogger<DisplayProvider> logger)
        {
            _wmiProvider = wmiProvider;
            _logger = logger;
        }

        public async Task<List<DisplayInformation>> GetDisplaysAsync(CancellationToken cancellationToken = default)
        {
            var list = new List<DisplayInformation>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    // Query resolution and refresh rate from WMI or Screen
                    var results = await _wmiProvider.QueryAsync(
                        "SELECT CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController",
                        "root\\CIMV2", cancellationToken);

                    foreach (var dict in results)
                    {
                        if (dict.TryGetValue("CurrentHorizontalResolution", out var horiz) &&
                            dict.TryGetValue("CurrentVerticalResolution", out var vert))
                        {
                            string resolution = $"{horiz}x{vert}";
                            double refreshRate = 60;
                            if (dict.TryGetValue("CurrentRefreshRate", out var rr) && rr != null)
                            {
                                double.TryParse(rr.ToString(), out refreshRate);
                            }

                            list.Add(new DisplayInformation(
                                resolution,
                                refreshRate,
                                Hdr: false, // Default fallback
                                Dpi: 96.0   // Standard 100% DPI fallback
                            ));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch display spec from WMI.");
                }
            }

            // Fallback default display if none found
            if (list.Count == 0)
            {
                list.Add(new DisplayInformation(
                    Resolution: "1920x1080",
                    RefreshRate: 144.0,
                    Hdr: true,
                    Dpi: 96.0
                ));
            }

            return list;
        }
    }
}
