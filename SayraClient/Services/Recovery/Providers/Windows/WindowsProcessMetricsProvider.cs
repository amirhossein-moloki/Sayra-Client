using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Recovery.Providers;

namespace SayraClient.Services.Recovery.Providers.Windows
{
    public class WindowsProcessMetricsProvider : IProcessMetricsProvider
    {
        private readonly ILogger<WindowsProcessMetricsProvider> _logger;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetGuiResources(IntPtr hProcess, int uiFlags);

        private const int GR_GDIOBJECTS = 0;

        public WindowsProcessMetricsProvider(ILogger<WindowsProcessMetricsProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<long> GetProcessRamBytesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var currentProc = Process.GetCurrentProcess();
                return Task.FromResult(currentProc.WorkingSet64);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query process working set RAM.");
                return Task.FromResult(250 * 1024 * 1024L); // 250 MB default fallback
            }
        }

        public Task<int> GetHandleCountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var currentProc = Process.GetCurrentProcess();
                return Task.FromResult(currentProc.HandleCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query process handle count.");
                return Task.FromResult(300); // default fallback
            }
        }

        public Task<int> GetThreadCountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var currentProc = Process.GetCurrentProcess();
                return Task.FromResult(currentProc.Threads.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query process thread count.");
                return Task.FromResult(25); // default fallback
            }
        }

        public Task<int> GetGdiObjectsCountAsync(CancellationToken cancellationToken = default)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Task.FromResult(120); // standard fallback for CI/Linux
            }

            try
            {
                using var currentProc = Process.GetCurrentProcess();
                int gdi = GetGuiResources(currentProc.Handle, GR_GDIOBJECTS);
                if (gdi >= 0)
                {
                    return Task.FromResult(gdi);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query GDI objects count on Windows.");
            }

            return Task.FromResult(120); // fallback
        }
    }
}
