using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Telemetry.Diagnostics.Modules
{
    public class RuntimeDiagnosticModule : IDiagnosticModule
    {
        private readonly IServiceProvider? _serviceProvider;

        public RuntimeDiagnosticModule(IServiceProvider? serviceProvider = null)
        {
            _serviceProvider = serviceProvider;
        }

        public string Name => "Runtime";
        public string AffectedSubsystem => "Runtime";

        public Task<DiagnosticModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new DiagnosticModuleResult { ModuleName = Name, Status = DiagnosticHealthStatus.Healthy };

            try
            {
                // GC Info
                long totalAllocatedMemory = GC.GetTotalMemory(false);
                var gcInfo = GC.GetGCMemoryInfo();

                result.Data["AllocatedMemoryBytes"] = totalAllocatedMemory.ToString();
                result.Data["AllocatedMemoryMb"] = (totalAllocatedMemory / (1024.0 * 1024)).ToString("F2");
                result.Data["GCCountGen0"] = GC.CollectionCount(0).ToString();
                result.Data["GCCountGen1"] = GC.CollectionCount(1).ToString();
                result.Data["GCCountGen2"] = GC.CollectionCount(2).ToString();
                result.Data["GCPromotedBytes"] = gcInfo.PromotedBytes.ToString();
                result.Data["GCHeapSizeBytes"] = gcInfo.HeapSizeBytes.ToString();

                // ThreadPool Info
                ThreadPool.GetMaxThreads(out int maxWorker, out int maxIocp);
                ThreadPool.GetAvailableThreads(out int availWorker, out int availIocp);
                int activeWorker = maxWorker - availWorker;
                int activeIocp = maxIocp - availIocp;

                result.Data["MaxWorkerThreads"] = maxWorker.ToString();
                result.Data["AvailableWorkerThreads"] = availWorker.ToString();
                result.Data["ActiveWorkerThreads"] = activeWorker.ToString();
                result.Data["MaxCompletionPortThreads"] = maxIocp.ToString();
                result.Data["AvailableCompletionPortThreads"] = availIocp.ToString();
                result.Data["ActiveCompletionPortThreads"] = activeIocp.ToString();

                // Worker Health Monitoring via reflection to avoid circular dependency
                bool allWorkersHealthy = true;
                if (_serviceProvider != null)
                {
                    try
                    {
                        var healthMonitorType = Type.GetType("SayraClient.Services.IServiceHealthMonitor, SayraClient")
                            ?? Type.GetType("SayraClient.Services.IServiceHealthMonitor");

                        if (healthMonitorType != null)
                        {
                            var healthMonitor = _serviceProvider.GetService(healthMonitorType);
                            if (healthMonitor != null)
                            {
                                var method = healthMonitor.GetType().GetMethod("IsHealthy");
                                if (method != null)
                                {
                                    allWorkersHealthy = (bool)method.Invoke(healthMonitor, null)!;
                                    result.Data["ServiceHealthMonitorStatus"] = allWorkersHealthy ? "Healthy" : "Degraded";
                                }
                            }
                        }
                    }
                    catch
                    {
                        result.Data["ServiceHealthMonitorStatus"] = "ErrorResolving";
                    }
                }

                if (!result.Data.ContainsKey("ServiceHealthMonitorStatus"))
                {
                    result.Data["ServiceHealthMonitorStatus"] = "NotRegistered";
                }

                // Findings & Evaluation rules
                if (activeWorker > maxWorker * 0.85)
                {
                    result.Status = DiagnosticHealthStatus.Degraded;
                    result.Errors.Add("ThreadPool starvation detected: over 85% of worker threads are active.");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "StarvedThreadPool",
                        Value = $"{activeWorker}/{maxWorker}",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "ThreadPool active threads exceeded 85% of physical limits."
                    });
                }
                else if (activeWorker > maxWorker * 0.5)
                {
                    if (result.Status < DiagnosticHealthStatus.Warning) result.Status = DiagnosticHealthStatus.Warning;
                    result.Warnings.Add("Elevated ThreadPool active thread count.");
                }

                // Low memory pressure check within GC
                if (totalAllocatedMemory > 1.5 * 1024 * 1024 * 1024L) // 1.5GB
                {
                    if (result.Status < DiagnosticHealthStatus.Warning) result.Status = DiagnosticHealthStatus.Warning;
                    result.Warnings.Add($"High CLR heap usage: {result.Data["AllocatedMemoryMb"]} MB.");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "HighCpuHeap",
                        Value = $"{result.Data["AllocatedMemoryMb"]} MB",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "CLR allocated GC heap size exceeded 1.5GB."
                    });
                }

                if (!allWorkersHealthy)
                {
                    result.Status = DiagnosticHealthStatus.Degraded;
                    result.Errors.Add("One or more background hosted worker services is in an unhealthy state.");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "UnhealthyWorkers",
                        Value = "Unhealthy",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "ServiceHealthMonitor detected hosted background service crashes or heartbeat misses."
                    });
                }
            }
            catch (Exception ex)
            {
                result.Status = DiagnosticHealthStatus.Unknown;
                result.Errors.Add($"Runtime diagnostics failed: {ex.Message}");
            }

            return Task.FromResult(result);
        }
    }
}
