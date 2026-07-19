using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Services
{
    public class HardwareTelemetryService : IHardwareTelemetryService
    {
        private readonly IPerformanceCounterProvider _perfProvider;
        private readonly IGpuProvider _gpuProvider;
        private readonly INetworkProvider _networkProvider;
        private readonly ILogger<HardwareTelemetryService> _logger;
        private readonly Process _currentProcess;

        public HardwareTelemetryService(
            IPerformanceCounterProvider perfProvider,
            IGpuProvider gpuProvider,
            INetworkProvider networkProvider,
            ILogger<HardwareTelemetryService> logger)
        {
            _perfProvider = perfProvider;
            _gpuProvider = gpuProvider;
            _networkProvider = networkProvider;
            _logger = logger;
            _currentProcess = Process.GetCurrentProcess();
        }

        public async Task<HardwareMetrics> GetLiveMetricsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Collecting live hardware telemetry metrics...");

            double cpuUsage = _perfProvider.GetCpuUsage();
            double ramUsage = _perfProvider.GetRamUsage();

            double gpuUsage = await _gpuProvider.GetGpuUsageAsync(cancellationToken);
            double vramUsage = await _gpuProvider.GetVramUsageAsync(cancellationToken);

            double diskUsage = _perfProvider.GetDiskUsage();
            double networkUsage = await _networkProvider.GetNetworkUsageAsync(cancellationToken);

            TimeSpan uptime = DateTime.Now - _currentProcess.StartTime;

            var activeProcesses = new List<string>();
            try
            {
                var processes = Process.GetProcesses();
                // Take top 5 memory processes as a clean representation
                activeProcesses = processes
                    .OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0L; } })
                    .Take(10)
                    .Select(p => p.ProcessName)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to compile active processes list.");
            }

            double refreshRate = 144.0; // Fallback
            double fpsCapability = 144.0; // Fallback detectable

            return new HardwareMetrics(
                cpuUsage,
                gpuUsage,
                ramUsage,
                vramUsage,
                diskUsage,
                networkUsage,
                uptime,
                activeProcesses,
                refreshRate,
                fpsCapability
            );
        }
    }
}
