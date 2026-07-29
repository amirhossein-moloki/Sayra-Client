using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Recovery.Providers;

namespace SayraClient.Services.Recovery.Providers.Windows
{
    public class WindowsDiskMetricsProvider : IDiskMetricsProvider
    {
        private readonly ILogger<WindowsDiskMetricsProvider> _logger;
        private readonly double _simulatedDiskIo = 1024 * 50; // 50 KB/s baseline

        public WindowsDiskMetricsProvider(ILogger<WindowsDiskMetricsProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<long> GetFreeDiskSpaceBytesAsync(string path, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetPath = string.IsNullOrWhiteSpace(path) ? AppContext.BaseDirectory : path;
                var root = Path.GetPathRoot(targetPath);
                if (string.IsNullOrWhiteSpace(root))
                {
                    root = "C";
                }
                var drive = new DriveInfo(root);
                if (drive.IsReady)
                {
                    return Task.FromResult(drive.AvailableFreeSpace);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch free disk space for path: {Path}. Using default.", path);
            }

            return Task.FromResult(10 * 1024 * 1024 * 1024L); // 10 GB fallback
        }

        public Task<double> GetDiskIoBytesPerSecondAsync(CancellationToken cancellationToken = default)
        {
            // Lightweight simulated baseline representing standard background activity
            return Task.FromResult(_simulatedDiskIo);
        }
    }
}
