using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Infrastructure.Windows.ResourceMonitoring
{
    public class ProcessResourceMonitor : IProcessResourceMonitor
    {
        private readonly ILogger<ProcessResourceMonitor> _logger;

        public ProcessResourceMonitor(ILogger<ProcessResourceMonitor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ResourceMetrics> MonitorMetricsAsync(int processId)
        {
            _logger.LogDebug("Querying resource metrics for process {ProcessId}", processId);

            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    if (process.HasExited)
                    {
                        throw new InvalidOperationException($"Process {processId} has already exited.");
                    }

                    // 1. Measure CPU usage over a short interval (e.g. 50ms)
                    var startCpuTime = process.TotalProcessorTime;
                    var startTime = DateTime.UtcNow;

                    await Task.Delay(50);

                    process.Refresh();
                    if (process.HasExited)
                    {
                        throw new InvalidOperationException($"Process {processId} has exited during metric query.");
                    }

                    var endCpuTime = process.TotalProcessorTime;
                    var endTime = DateTime.UtcNow;

                    var cpuUsedMs = (endCpuTime - startCpuTime).TotalMilliseconds;
                    var totalMs = (endTime - startTime).TotalMilliseconds * Environment.ProcessorCount;

                    double cpuUsage = totalMs > 0 ? (cpuUsedMs / totalMs) * 100.0 : 0.0;
                    if (cpuUsage < 0) cpuUsage = 0;
                    if (cpuUsage > 100) cpuUsage = 100;

                    // 2. Read memory and handles
                    long memoryUsage = process.WorkingSet64;
                    int handles = process.HandleCount;

                    return new ResourceMetrics
                    {
                        CpuUsagePercentage = Math.Round(cpuUsage, 2),
                        MemoryUsageBytes = memoryUsage,
                        HandleCount = handles
                    };
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Process {ProcessId} was not found.", processId);
                throw new InvalidOperationException($"Process {processId} was not found.", ex);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                _logger.LogError(ex, "Failed to monitor metrics for process {ProcessId}", processId);
                throw new InvalidOperationException($"Failed to query metrics for process {processId}", ex);
            }
        }
    }
}
