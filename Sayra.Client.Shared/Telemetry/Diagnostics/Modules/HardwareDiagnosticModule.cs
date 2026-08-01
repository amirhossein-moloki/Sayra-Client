using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;

namespace Sayra.Client.Shared.Telemetry.Diagnostics.Modules
{
    public class HardwareDiagnosticModule : IDiagnosticModule
    {
        private readonly IResourceMonitor? _resourceMonitor;
        private readonly IHardwareSensorProvider? _sensorProvider;

        public HardwareDiagnosticModule(IResourceMonitor? resourceMonitor = null, IHardwareSensorProvider? sensorProvider = null)
        {
            _resourceMonitor = resourceMonitor;
            _sensorProvider = sensorProvider;
        }

        public string Name => "Hardware";
        public string AffectedSubsystem => "Hardware";

        public async Task<DiagnosticModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new DiagnosticModuleResult { ModuleName = Name, Status = DiagnosticHealthStatus.Healthy };

            try
            {
                double cpu = 0.0;
                long totalRam = 8L * 1024 * 1024 * 1024; // 8GB default fallback
                long availRam = 4L * 1024 * 1024 * 1024; // 4GB default fallback
                long freeDisk = 100L * 1024 * 1024 * 1024; // 100GB default fallback
                double gpu = 0.0;
                double cpuTemp = 45.0;
                double gpuTemp = 50.0;

                if (_resourceMonitor != null)
                {
                    var metrics = await _resourceMonitor.GetCurrentMetricsAsync(cancellationToken);
                    cpu = metrics.CpuUsagePercentage;
                    totalRam = metrics.TotalSystemRamBytes > 0 ? metrics.TotalSystemRamBytes : totalRam;
                    availRam = metrics.AvailableSystemRamBytes > 0 ? metrics.AvailableSystemRamBytes : availRam;
                    freeDisk = metrics.FreeDiskSpaceBytes > 0 ? metrics.FreeDiskSpaceBytes : freeDisk;
                    gpu = metrics.GpuUsagePercentage;
                }

                if (_sensorProvider != null)
                {
                    cpuTemp = _sensorProvider.GetCpuTemperature();
                    gpuTemp = _sensorProvider.GetGpuTemperature();
                    result.Data["FanSpeedRpm"] = _sensorProvider.GetFanSpeed().ToString("F0");
                }

                result.Data["CpuUsagePercent"] = cpu.ToString("F1");
                result.Data["TotalRamGb"] = (totalRam / (1024.0 * 1024 * 1024)).ToString("F2");
                result.Data["AvailableRamGb"] = (availRam / (1024.0 * 1024 * 1024)).ToString("F2");
                result.Data["FreeDiskGb"] = (freeDisk / (1024.0 * 1024 * 1024)).ToString("F2");
                result.Data["GpuUsagePercent"] = gpu.ToString("F1");
                result.Data["CpuTempCelsius"] = cpuTemp.ToString("F1");
                result.Data["GpuTempCelsius"] = gpuTemp.ToString("F1");

                // Uptime evaluation
                var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                result.Data["SystemUptime"] = uptime.ToString(@"dd\.hh\:mm\:ss");

                // Findings & Evaluation rules
                if (cpu > 90.0)
                {
                    result.Status = DiagnosticHealthStatus.Degraded;
                    result.Errors.Add($"CPU usage is extremely high: {cpu:F1}%");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "CpuUsageLimitExceeded",
                        Value = $"{cpu:F1}%",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "CPU core load exceeds 90% threshold limit."
                    });
                }
                else if (cpu > 75.0)
                {
                    if (result.Status < DiagnosticHealthStatus.Warning) result.Status = DiagnosticHealthStatus.Warning;
                    result.Warnings.Add($"CPU usage is elevated: {cpu:F1}%");
                }

                if (availRam < 512 * 1024 * 1024L) // < 512MB
                {
                    result.Status = DiagnosticHealthStatus.Critical;
                    result.Errors.Add($"System physical memory is nearly exhausted: {result.Data["AvailableRamGb"]} GB available.");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "LowAvailableRam",
                        Value = $"{result.Data["AvailableRamGb"]} GB",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "Physical available RAM fell below critical 512MB threshold."
                    });
                }
                else if (availRam < 1.5 * 1024 * 1024 * 1024L) // < 1.5GB
                {
                    if (result.Status < DiagnosticHealthStatus.Warning) result.Status = DiagnosticHealthStatus.Warning;
                    result.Warnings.Add($"System available memory is low: {result.Data["AvailableRamGb"]} GB left.");
                }

                if (freeDisk < 10L * 1024 * 1024 * 1024) // < 10GB
                {
                    if (result.Status < DiagnosticHealthStatus.Warning) result.Status = DiagnosticHealthStatus.Warning;
                    result.Warnings.Add($"Primary storage free space is low: {result.Data["FreeDiskGb"]} GB left.");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "LowFreeSpace",
                        Value = $"{result.Data["FreeDiskGb"]} GB",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "Free primary disk space is under 10GB."
                    });
                }

                if (cpuTemp > 85.0 || gpuTemp > 85.0)
                {
                    if (result.Status < DiagnosticHealthStatus.Warning) result.Status = DiagnosticHealthStatus.Warning;
                    result.Warnings.Add($"Elevated hardware temperature detected: CPU={cpuTemp:F1}°C, GPU={gpuTemp:F1}°C");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "HighHardwareTemp",
                        Value = $"CPU={cpuTemp:F1}°C, GPU={gpuTemp:F1}°C",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "Hardware temperatures exceeded thermal safe bounds."
                    });
                }
            }
            catch (Exception ex)
            {
                result.Status = DiagnosticHealthStatus.Unknown;
                result.Errors.Add($"Hardware diagnostics failed: {ex.Message}");
            }

            return result;
        }
    }
}
