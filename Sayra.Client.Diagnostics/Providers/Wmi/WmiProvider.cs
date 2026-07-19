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
    public class WmiProvider : IWmiProvider
    {
        private readonly ILogger<WmiProvider> _logger;

        public string ProviderName => "WMI Provider";

        public WmiProvider(ILogger<WmiProvider> logger)
        {
            _logger = logger;
        }

        public Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ValidationResult(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), new(), new()));
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
                ExecuteWmiOnWindows(query, scope, results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WMI query failed: '{Query}' in scope '{Scope}'", query, scope);
            }

            return Task.FromResult(results);
        }

        private void ExecuteWmiOnWindows(string query, string scope, List<Dictionary<string, object>> results)
        {
#if NET8_0_OR_GREATER
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
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
