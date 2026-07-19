using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Providers
{
    public class VulkanProvider : IVulkanProvider
    {
        private readonly ILogger<VulkanProvider> _logger;

        public string ProviderName => "Vulkan Provider";

        public VulkanProvider(ILogger<VulkanProvider> logger)
        {
            _logger = logger;
        }

        public Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ValidationResult(true, new(), new()));
        }

        public Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Vulkan Provider started.");

            string version = "1.3.204"; // Default fallback
            bool supported = IsSupportedInternal();
            if (!supported)
            {
                version = "Not Supported";
            }

            _logger.LogInformation("Vulkan Provider completed in {DurationMs} ms.", sw.ElapsedMilliseconds);
            return Task.FromResult(version);
        }

        public Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsSupportedInternal());
        }

        private bool IsSupportedInternal()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string vulkanDllPath = Path.Combine(systemPath, "vulkan-1.dll");
                    return File.Exists(vulkanDllPath);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to verify Vulkan support via vulkan-1.dll existence check.");
                    return false;
                }
            }
            return false;
        }
    }
}
