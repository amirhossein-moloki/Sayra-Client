using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces.Providers;

namespace Sayra.Client.Diagnostics.Providers
{
    public class WmiProvider : IWmiProvider
    {
        private readonly ILogger<WmiProvider> _logger;

        public WmiProvider(ILogger<WmiProvider> logger)
        {
            _logger = logger;
        }

        public Task<List<Dictionary<string, object>>> QueryAsync(string query, string scope = "root\\CIMV2", CancellationToken cancellationToken = default)
        {
            var results = new List<Dictionary<string, object>>();

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("WMI queries are only supported on Windows. Falling back with empty result set.");
                return Task.FromResult(results);
            }

            try
            {
                // Execute WMI on Windows
                ExecuteWmiOnWindows(query, scope, results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WMI query failed: '{Query}' in scope '{Scope}'", query, scope);
            }

            return Task.FromResult(results);
        }

        // Keep this in a separate method to prevent JIT load issues of System.Management on Linux/Mac
        private void ExecuteWmiOnWindows(string query, string scope, List<Dictionary<string, object>> results)
        {
#if NET8_0_OR_GREATER
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Suppress CA1416 warning locally if needed, but standard guard is sufficient.
                using (var searcher = new System.Management.ManagementObjectSearcher(scope, query))
                using (var collection = searcher.Get())
                {
                    foreach (System.Management.ManagementObject obj in collection)
                    {
                        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in obj.Properties)
                        {
                            dict[prop.Name] = prop.Value ?? string.Empty;
                        }
                        results.Add(dict);
                    }
                }
            }
#endif
        }
    }
}
