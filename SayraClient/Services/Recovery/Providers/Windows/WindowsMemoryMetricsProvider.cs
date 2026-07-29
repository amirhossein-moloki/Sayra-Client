using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Recovery.Providers;

namespace SayraClient.Services.Recovery.Providers.Windows
{
    public class WindowsMemoryMetricsProvider : IMemoryMetricsProvider
    {
        private readonly ILogger<WindowsMemoryMetricsProvider> _logger;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public WindowsMemoryMetricsProvider(ILogger<WindowsMemoryMetricsProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<long> GetTotalSystemRamBytesAsync(CancellationToken cancellationToken = default)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Task.FromResult(16106127360L); // 16 GB fallback for non-Windows/CI
            }

            try
            {
                var stat = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (GlobalMemoryStatusEx(ref stat))
                {
                    return Task.FromResult((long)stat.ullTotalPhys);
                }
                _logger.LogWarning("GlobalMemoryStatusEx failed. Code: {ErrorCode}", Marshal.GetLastWin32Error());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get total system RAM bytes.");
            }

            return Task.FromResult(16106127360L); // fallback
        }

        public Task<long> GetAvailableSystemRamBytesAsync(CancellationToken cancellationToken = default)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Task.FromResult(8589934592L); // 8 GB fallback for non-Windows/CI
            }

            try
            {
                var stat = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (GlobalMemoryStatusEx(ref stat))
                {
                    return Task.FromResult((long)stat.ullAvailPhys);
                }
                _logger.LogWarning("GlobalMemoryStatusEx failed. Code: {ErrorCode}", Marshal.GetLastWin32Error());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available system RAM bytes.");
            }

            return Task.FromResult(8589934592L); // fallback
        }
    }
}
