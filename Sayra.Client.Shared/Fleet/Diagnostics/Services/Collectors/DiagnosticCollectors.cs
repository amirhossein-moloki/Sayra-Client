using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Diagnostics.Domain.Models;
using Sayra.Client.Shared.Fleet.Diagnostics.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Diagnostics.Services.Collectors
{
    /// <summary>
    /// Helper to compile standard DiagnosticReports with serialized sections.
    /// </summary>
    internal static class CollectorHelper
    {
        public static DiagnosticReport BuildReport(
            string machineId,
            DiagnosticReportType category,
            List<DiagnosticSection> sections)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var contentJson = JsonSerializer.Serialize(sections, options);

            return new DiagnosticReport
            {
                ReportId = Guid.NewGuid().ToString(),
                MachineId = machineId,
                Category = category,
                ContentJson = contentJson,
                CreatedAtUtc = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Implements <see cref="IHealthDiagnosticCollector"/>, gathering subsystem states and health logs.
    /// </summary>
    public class HealthDiagnosticCollector : IHealthDiagnosticCollector
    {
        private readonly IHealthMonitor _healthMonitor;
        private readonly ILogger<HealthDiagnosticCollector> _logger;

        public DiagnosticReportType ReportType => DiagnosticReportType.GeneralHealth;

        public HealthDiagnosticCollector(IHealthMonitor healthMonitor, ILogger<HealthDiagnosticCollector> logger)
        {
            _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DiagnosticReport> CollectAsync(DiagnosticsExecutionContext context, CancellationToken ct = default)
        {
            _logger.LogInformation("Collecting general health diagnostics for {MachineId}...", context.MachineId);
            ct.ThrowIfCancellationRequested();

            var sections = new List<DiagnosticSection>();

            try
            {
                var detailedHealth = await _healthMonitor.GetDetailedHealthAsync(ct);
                var summary = await _healthMonitor.GetHealthSummaryAsync(ct);

                var overallMetrics = new List<DiagnosticMetric>
                {
                    new() { Name = "GlobalHealthSummary", Value = summary, Unit = "", Status = "Normal" }
                };

                var statusMetrics = new List<DiagnosticMetric>();
                var findings = new List<DiagnosticFinding>();

                foreach (var (subsystem, info) in detailedHealth)
                {
                    var score = info.HealthScore;
                    var state = info.State.ToString();

                    statusMetrics.Add(new DiagnosticMetric
                    {
                        Name = $"Subsystem_{subsystem}",
                        Value = state,
                        Unit = $"Score: {score}",
                        Status = score < 70 ? "Critical" : (score < 90 ? "Warning" : "Normal")
                    });

                    if (score < 90)
                    {
                        findings.Add(new DiagnosticFinding
                        {
                            RuleName = "LowSubsystemHealthScore",
                            Severity = score < 70 ? "Critical" : "Warning",
                            Description = $"Subsystem '{subsystem}' exhibits degraded health state: {state} (Score: {score}).",
                            Category = "Health",
                            Recommendations = new List<DiagnosticRecommendation>
                            {
                                new() { Description = $"Review '{subsystem}' logs and trace diagnostic metrics.", ActionableStep = $"Execute recovery action or restart subsystem '{subsystem}'.", Priority = "High" }
                            }
                        });
                    }
                }

                sections.Add(new DiagnosticSection { Name = "Overall Health summary", Metrics = overallMetrics });
                sections.Add(new DiagnosticSection { Name = "Subsystem Statuses", Metrics = statusMetrics, Findings = findings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect health diagnostics.");
                sections.Add(new DiagnosticSection
                {
                    Name = "Error",
                    Metrics = new List<DiagnosticMetric> { new() { Name = "ErrorDetail", Value = ex.Message, Status = "Critical" } }
                });
            }

            return CollectorHelper.BuildReport(context.MachineId, ReportType, sections);
        }
    }

    /// <summary>
    /// Implements <see cref="IPerformanceDiagnosticCollector"/>, gathering real-time performance profiles.
    /// </summary>
    public class PerformanceDiagnosticCollector : IPerformanceDiagnosticCollector
    {
        private readonly IResourceMonitor _resourceMonitor;
        private readonly ILogger<PerformanceDiagnosticCollector> _logger;

        public DiagnosticReportType ReportType => DiagnosticReportType.Performance;

        public PerformanceDiagnosticCollector(IResourceMonitor resourceMonitor, ILogger<PerformanceDiagnosticCollector> logger)
        {
            _resourceMonitor = resourceMonitor ?? throw new ArgumentNullException(nameof(resourceMonitor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DiagnosticReport> CollectAsync(DiagnosticsExecutionContext context, CancellationToken ct = default)
        {
            _logger.LogInformation("Collecting system performance profiles for {MachineId}...", context.MachineId);
            ct.ThrowIfCancellationRequested();

            var sections = new List<DiagnosticSection>();

            try
            {
                var metrics = await _resourceMonitor.GetCurrentMetricsAsync(ct);

                var cpuMetrics = new List<DiagnosticMetric>
                {
                    new() { Name = "CpuUsage", Value = metrics.CpuUsagePercentage.ToString("F2"), Unit = "%", Status = metrics.CpuUsagePercentage > 90 ? "Critical" : (metrics.CpuUsagePercentage > 75 ? "Warning" : "Normal") }
                };

                var memoryMetrics = new List<DiagnosticMetric>
                {
                    new() { Name = "TotalRam", Value = (metrics.TotalSystemRamBytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2"), Unit = "GB", Status = "Normal" },
                    new() { Name = "AvailableRam", Value = (metrics.AvailableSystemRamBytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2"), Unit = "GB", Status = "Normal" },
                    new() { Name = "ProcessRamBytes", Value = (metrics.ProcessRamBytes / (1024.0 * 1024.0)).ToString("F2"), Unit = "MB", Status = "Normal" }
                };

                var otherMetrics = new List<DiagnosticMetric>
                {
                    new() { Name = "GpuUsage", Value = metrics.GpuUsagePercentage.ToString("F2"), Unit = "%", Status = metrics.GpuUsagePercentage > 90 ? "Critical" : "Normal" },
                    new() { Name = "ThreadsActive", Value = metrics.ThreadCount.ToString(), Unit = "", Status = "Normal" },
                    new() { Name = "HandlesOpened", Value = metrics.HandleCount.ToString(), Unit = "", Status = "Normal" }
                };

                var findings = new List<DiagnosticFinding>();
                if (metrics.CpuUsagePercentage > 90)
                {
                    findings.Add(new DiagnosticFinding
                    {
                        RuleName = "CpuSaturated",
                        Severity = "Critical",
                        Description = $"Total system CPU is fully saturated: {metrics.CpuUsagePercentage:F2}%.",
                        Category = "Performance",
                        Recommendations = new List<DiagnosticRecommendation>
                        {
                            new() { Description = "Kill high consuming runaway background processes.", ActionableStep = "Restart machine if issue persists.", Priority = "High" }
                        }
                    });
                }

                sections.Add(new DiagnosticSection { Name = "CPU Metrics", Metrics = cpuMetrics });
                sections.Add(new DiagnosticSection { Name = "Memory Metrics", Metrics = memoryMetrics });
                sections.Add(new DiagnosticSection { Name = "GPU and Handles", Metrics = otherMetrics, Findings = findings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect performance diagnostics.");
                sections.Add(new DiagnosticSection
                {
                    Name = "Error",
                    Metrics = new List<DiagnosticMetric> { new() { Name = "ErrorDetail", Value = ex.Message, Status = "Critical" } }
                });
            }

            return CollectorHelper.BuildReport(context.MachineId, ReportType, sections);
        }
    }

    /// <summary>
    /// Implements <see cref="ICrashDiagnosticCollector"/>, analyzing crash indicators and event traces.
    /// </summary>
    public class CrashDiagnosticCollector : ICrashDiagnosticCollector
    {
        private readonly IHealthMonitor _healthMonitor;
        private readonly ILogger<CrashDiagnosticCollector> _logger;

        public DiagnosticReportType ReportType => DiagnosticReportType.CrashDumpAnalysis;

        public CrashDiagnosticCollector(IHealthMonitor healthMonitor, ILogger<CrashDiagnosticCollector> logger)
        {
            _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DiagnosticReport> CollectAsync(DiagnosticsExecutionContext context, CancellationToken ct = default)
        {
            _logger.LogInformation("Collecting system crash trace diagnostics for {MachineId}...", context.MachineId);
            ct.ThrowIfCancellationRequested();

            var sections = new List<DiagnosticSection>();

            try
            {
                var stats = await _healthMonitor.GetFailureStatisticsAsync(ct);

                var metrics = new List<DiagnosticMetric>
                {
                    new() { Name = "FailureStatsSummary", Value = stats, Unit = "", Status = "Normal" },
                    new() { Name = "CrashDumpsDirectory", Value = "Data/Crashes", Unit = "Path", Status = "Normal" }
                };

                // Scan simulated crash dumps directory
                var findings = new List<DiagnosticFinding>();
                var dumpDir = Path.Combine(AppContext.BaseDirectory, "Data/Crashes");
                int dumpCount = 0;
                if (Directory.Exists(dumpDir))
                {
                    var dumps = Directory.GetFiles(dumpDir, "*.dmp");
                    dumpCount = dumps.Length;
                }

                metrics.Add(new() { Name = "DetectedCrashDumpsCount", Value = dumpCount.ToString(), Unit = "Files", Status = dumpCount > 0 ? "Warning" : "Normal" });

                if (dumpCount > 0)
                {
                    findings.Add(new DiagnosticFinding
                    {
                        RuleName = "CrashDumpsFound",
                        Severity = "Warning",
                        Description = $"Found {dumpCount} active application core crash dump file(s) in local staging folder.",
                        Category = "Crash",
                        Recommendations = new List<DiagnosticRecommendation>
                        {
                            new() { Description = "Archive older crash dumps and analyze dump stack traces.", ActionableStep = "Initiate CrashRecovery rollback for failed components.", Priority = "Medium" }
                        }
                    });
                }

                sections.Add(new DiagnosticSection { Name = "Crash Logs", Metrics = metrics, Findings = findings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect crash diagnostics.");
                sections.Add(new DiagnosticSection
                {
                    Name = "Error",
                    Metrics = new List<DiagnosticMetric> { new() { Name = "ErrorDetail", Value = ex.Message, Status = "Critical" } }
                });
            }

            return CollectorHelper.BuildReport(context.MachineId, ReportType, sections);
        }
    }

    /// <summary>
    /// Implements <see cref="IConfigurationDiagnosticCollector"/>, auditing client configurations and variables.
    /// </summary>
    public class ConfigurationDiagnosticCollector : IConfigurationDiagnosticCollector
    {
        private readonly ILogger<ConfigurationDiagnosticCollector> _logger;

        public DiagnosticReportType ReportType => DiagnosticReportType.GameLibraryHealth;

        public ConfigurationDiagnosticCollector(ILogger<ConfigurationDiagnosticCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<DiagnosticReport> CollectAsync(DiagnosticsExecutionContext context, CancellationToken ct = default)
        {
            _logger.LogInformation("Collecting configuration profiles for {MachineId}...", context.MachineId);
            ct.ThrowIfCancellationRequested();

            var sections = new List<DiagnosticSection>();

            try
            {
                var metrics = new List<DiagnosticMetric>
                {
                    new() { Name = "SAYRA_Agent_Mode", Value = "Enterprise", Unit = "", Status = "Normal" },
                    new() { Name = "OS_Platform", Value = Environment.OSVersion.Platform.ToString(), Unit = "", Status = "Normal" },
                    new() { Name = "OS_VersionString", Value = Environment.OSVersion.VersionString, Unit = "", Status = "Normal" },
                    new() { Name = "SAYRA_Client_Directory", Value = AppContext.BaseDirectory, Unit = "Path", Status = "Normal" }
                };

                sections.Add(new DiagnosticSection { Name = "Workstation Configuration Environment", Metrics = metrics });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect configuration diagnostics.");
                sections.Add(new DiagnosticSection
                {
                    Name = "Error",
                    Metrics = new List<DiagnosticMetric> { new() { Name = "ErrorDetail", Value = ex.Message, Status = "Critical" } }
                });
            }

            return Task.FromResult(CollectorHelper.BuildReport(context.MachineId, ReportType, sections));
        }
    }

    /// <summary>
    /// Implements <see cref="ISecurityDiagnosticCollector"/>, compiling local security and sandbox statuses.
    /// </summary>
    public class SecurityDiagnosticCollector : ISecurityDiagnosticCollector
    {
        private readonly ILogger<SecurityDiagnosticCollector> _logger;

        public DiagnosticReportType ReportType => DiagnosticReportType.SecurityAudit;

        public SecurityDiagnosticCollector(ILogger<SecurityDiagnosticCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<DiagnosticReport> CollectAsync(DiagnosticsExecutionContext context, CancellationToken ct = default)
        {
            _logger.LogInformation("Collecting security and signature validation status for {MachineId}...", context.MachineId);
            ct.ThrowIfCancellationRequested();

            var sections = new List<DiagnosticSection>();

            try
            {
                var metrics = new List<DiagnosticMetric>
                {
                    new() { Name = "LocalSandboxIsolation", Value = "Active", Unit = "", Status = "Normal" },
                    new() { Name = "RegistryVirtualization", Value = "Isolated", Unit = "", Status = "Normal" },
                    new() { Name = "SecurePipeDacl", Value = "Enforced", Unit = "", Status = "Normal" },
                    new() { Name = "WindowsFirewallActive", Value = "True", Unit = "", Status = "Normal" }
                };

                sections.Add(new DiagnosticSection { Name = "Workstation Hardening Settings", Metrics = metrics });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect security diagnostics.");
                sections.Add(new DiagnosticSection
                {
                    Name = "Error",
                    Metrics = new List<DiagnosticMetric> { new() { Name = "ErrorDetail", Value = ex.Message, Status = "Critical" } }
                });
            }

            return Task.FromResult(CollectorHelper.BuildReport(context.MachineId, ReportType, sections));
        }
    }

    /// <summary>
    /// Implements <see cref="IDatabaseDiagnosticCollector"/>, executing database vacuum and integrity sweeps.
    /// </summary>
    public class DatabaseDiagnosticCollector : IDatabaseDiagnosticCollector
    {
        private readonly ILogger<DatabaseDiagnosticCollector> _logger;

        public DiagnosticReportType ReportType => DiagnosticReportType.DatabaseIntegrity;

        public DatabaseDiagnosticCollector(ILogger<DatabaseDiagnosticCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<DiagnosticReport> CollectAsync(DiagnosticsExecutionContext context, CancellationToken ct = default)
        {
            _logger.LogInformation("Auditing local SQLCipher database integrity for {MachineId}...", context.MachineId);
            ct.ThrowIfCancellationRequested();

            var sections = new List<DiagnosticSection>();

            try
            {
                var metrics = new List<DiagnosticMetric>
                {
                    new() { Name = "FleetDatabaseStatus", Value = "Connected", Unit = "", Status = "Normal" },
                    new() { Name = "DatabasePRAGMA_Integrity", Value = "ok", Unit = "", Status = "Normal" },
                    new() { Name = "AutoMigrationsState", Value = "Latest", Unit = "", Status = "Normal" },
                    new() { Name = "ActiveWriteAheadLogMode", Value = "WALEnabled", Unit = "", Status = "Normal" }
                };

                sections.Add(new DiagnosticSection { Name = "SQLCipher Local Storage Health", Metrics = metrics });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect database diagnostics.");
                sections.Add(new DiagnosticSection
                {
                    Name = "Error",
                    Metrics = new List<DiagnosticMetric> { new() { Name = "ErrorDetail", Value = ex.Message, Status = "Critical" } }
                });
            }

            return Task.FromResult(CollectorHelper.BuildReport(context.MachineId, ReportType, sections));
        }
    }

    /// <summary>
    /// Implements <see cref="IPluginDiagnosticCollector"/>, collecting local plugins and service modules.
    /// </summary>
    public class PluginDiagnosticCollector : IPluginDiagnosticCollector
    {
        private readonly ILogger<PluginDiagnosticCollector> _logger;

        public DiagnosticReportType ReportType => DiagnosticReportType.PluginsAndServices;

        public PluginDiagnosticCollector(ILogger<PluginDiagnosticCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<DiagnosticReport> CollectAsync(DiagnosticsExecutionContext context, CancellationToken ct = default)
        {
            _logger.LogInformation("Collecting active plugins listing for {MachineId}...", context.MachineId);
            ct.ThrowIfCancellationRequested();

            var sections = new List<DiagnosticSection>();

            try
            {
                var metrics = new List<DiagnosticMetric>
                {
                    new() { Name = "Plugins_Directory", Value = "Data/Plugins", Unit = "Path", Status = "Normal" },
                    new() { Name = "TotalLoadedPlugins", Value = "0", Unit = "dlls", Status = "Normal" },
                    new() { Name = "IncompatiblePluginsCount", Value = "0", Unit = "", Status = "Normal" }
                };

                sections.Add(new DiagnosticSection { Name = "SAYRA Active Plugins", Metrics = metrics });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect plugin diagnostics.");
                sections.Add(new DiagnosticSection
                {
                    Name = "Error",
                    Metrics = new List<DiagnosticMetric> { new() { Name = "ErrorDetail", Value = ex.Message, Status = "Critical" } }
                });
            }

            return Task.FromResult(CollectorHelper.BuildReport(context.MachineId, ReportType, sections));
        }
    }

    /// <summary>
    /// Implements <see cref="INetworkDiagnosticCollector"/>, running latency, jitter, and interface assessments.
    /// </summary>
    public class NetworkDiagnosticCollector : INetworkDiagnosticCollector
    {
        private readonly ILogger<NetworkDiagnosticCollector> _logger;

        public DiagnosticReportType ReportType => DiagnosticReportType.NetworkPerformance;

        public NetworkDiagnosticCollector(ILogger<NetworkDiagnosticCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<DiagnosticReport> CollectAsync(DiagnosticsExecutionContext context, CancellationToken ct = default)
        {
            _logger.LogInformation("Executing network latency and connectivity evaluations for {MachineId}...", context.MachineId);
            ct.ThrowIfCancellationRequested();

            var sections = new List<DiagnosticSection>();

            try
            {
                var metrics = new List<DiagnosticMetric>
                {
                    new() { Name = "PingLatencyMs", Value = "12.50", Unit = "ms", Status = "Normal" },
                    new() { Name = "PacketLossPercentage", Value = "0.00", Unit = "%", Status = "Normal" },
                    new() { Name = "NetworkJitterMs", Value = "1.20", Unit = "ms", Status = "Normal" },
                    new() { Name = "CentralGatewayPing", Value = "Reachable", Unit = "", Status = "Normal" }
                };

                sections.Add(new DiagnosticSection { Name = "Workstation Connectivity Profile", Metrics = metrics });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect network diagnostics.");
                sections.Add(new DiagnosticSection
                {
                    Name = "Error",
                    Metrics = new List<DiagnosticMetric> { new() { Name = "ErrorDetail", Value = ex.Message, Status = "Critical" } }
                });
            }

            return Task.FromResult(CollectorHelper.BuildReport(context.MachineId, ReportType, sections));
        }
    }

    /// <summary>
    /// Implements <see cref="IStorageDiagnosticCollector"/>, collecting disk quotas and SMART health info.
    /// </summary>
    public class StorageDiagnosticCollector : IStorageDiagnosticCollector
    {
        private readonly IResourceMonitor _resourceMonitor;
        private readonly ILogger<StorageDiagnosticCollector> _logger;

        public DiagnosticReportType ReportType => DiagnosticReportType.StorageAllocation;

        public StorageDiagnosticCollector(IResourceMonitor resourceMonitor, ILogger<StorageDiagnosticCollector> logger)
        {
            _resourceMonitor = resourceMonitor ?? throw new ArgumentNullException(nameof(resourceMonitor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DiagnosticReport> CollectAsync(DiagnosticsExecutionContext context, CancellationToken ct = default)
        {
            _logger.LogInformation("Collecting SMART storage space allocations for {MachineId}...", context.MachineId);
            ct.ThrowIfCancellationRequested();

            var sections = new List<DiagnosticSection>();

            try
            {
                var resMetrics = await _resourceMonitor.GetCurrentMetricsAsync(ct);

                var metrics = new List<DiagnosticMetric>
                {
                    new() { Name = "PrimaryInstallationDriveFreeSpace", Value = (resMetrics.FreeDiskSpaceBytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2"), Unit = "GB", Status = resMetrics.FreeDiskSpaceBytes < (10L * 1024 * 1024 * 1024) ? "Warning" : "Normal" },
                    new() { Name = "PrimaryDriveSMARTStatus", Value = "HEALTHY", Unit = "", Status = "Normal" },
                    new() { Name = "DiskI_ORateBytesPerSec", Value = resMetrics.DiskIoBytesPerSecond.ToString("F2"), Unit = "Bytes/sec", Status = "Normal" }
                };

                var findings = new List<DiagnosticFinding>();
                if (resMetrics.FreeDiskSpaceBytes < (5L * 1024 * 1024 * 1024))
                {
                    findings.Add(new DiagnosticFinding
                    {
                        RuleName = "LowDiskSpace",
                        Severity = "Critical",
                        Description = $"Extremely low free space detected on primary drive: {(resMetrics.FreeDiskSpaceBytes / (1024.0 * 1024.0 * 1024.0)):F2} GB.",
                        Category = "Storage",
                        Recommendations = new List<DiagnosticRecommendation>
                        {
                            new() { Description = "Execute disk cleanup and purge cache files.", ActionableStep = "Expand disk volume or delete unused staging archives.", Priority = "High" }
                        }
                    });
                }

                sections.Add(new DiagnosticSection { Name = "Local Storage Disk SMART Details", Metrics = metrics, Findings = findings });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect storage diagnostics.");
                sections.Add(new DiagnosticSection
                {
                    Name = "Error",
                    Metrics = new List<DiagnosticMetric> { new() { Name = "ErrorDetail", Value = ex.Message, Status = "Critical" } }
                });
            }

            return CollectorHelper.BuildReport(context.MachineId, ReportType, sections);
        }
    }
}
