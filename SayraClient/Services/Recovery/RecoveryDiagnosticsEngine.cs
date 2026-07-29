using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;

namespace SayraClient.Services.Recovery
{
    public class RecoveryDiagnosticsEngine : IRecoveryDiagnosticsEngine
    {
        private readonly ILogger<RecoveryDiagnosticsEngine> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _reportsDir;

        public RecoveryDiagnosticsEngine(ILogger<RecoveryDiagnosticsEngine> logger, IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _reportsDir = Path.Combine(AppContext.BaseDirectory, "diagnostics_reports");
            Directory.CreateDirectory(_reportsDir);
        }

        public async Task GenerateAndPersistAllReportsAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Generating Phase 5 Stage 7 Diagnostics Reports...");

            await GenerateStartupReportAsync(ct);
            await GenerateHealthSummaryReportAsync(ct);
            await GenerateRecoveryReportAsync(ct);
            await GenerateFailureReportAsync(ct);

            _logger.LogInformation("All diagnostics reports successfully persisted under {Dir}", _reportsDir);
        }

        public async Task<string> GenerateStartupReportAsync(CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("SAYRA ENTERPRISE WINDOWS CLIENT: STARTUP REPORT");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
            sb.AppendLine("==================================================");

            // Fetch Database status
            var hardening = _serviceProvider.GetService<ISecurityHardeningService>();
            bool dbOk = false;
            if (hardening != null)
            {
                dbOk = await hardening.VerifyDatabaseIntegrityAsync(ct);
            }
            sb.AppendLine($"SQLCipher Database Status: {(dbOk ? "INTEGRITY_VERIFIED" : "INTEGRITY_COMPROMISED_OR_MISSING")}");

            // Fetch configuration existence
            bool configOk = File.Exists(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
            sb.AppendLine($"Configuration File (appsettings.json) Present: {configOk}");

            // Operating System environment
            sb.AppendLine($"64-Bit OS: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"64-Bit Process: {Environment.Is64BitProcess}");
            sb.AppendLine($"Machine Name: {Environment.MachineName}");
            sb.AppendLine($"OS Version: {Environment.OSVersion}");
            sb.AppendLine("==================================================");

            string content = sb.ToString();
            await File.WriteAllTextAsync(Path.Combine(_reportsDir, "startup_report.txt"), content, ct);
            return content;
        }

        public async Task<string> GenerateHealthSummaryReportAsync(CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("SAYRA ENTERPRISE WINDOWS CLIENT: HEALTH SUMMARY REPORT");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
            sb.AppendLine("==================================================");

            var monitor = _serviceProvider.GetService<IHealthMonitor>();
            if (monitor != null)
            {
                var detailed = monitor.GetDetailedHealth();
                foreach (var kvp in detailed)
                {
                    var sub = kvp.Value;
                    sb.AppendLine($"Subsystem: {sub.SubsystemName}");
                    sb.AppendLine($"  State: {sub.State}");
                    sb.AppendLine($"  Last Heartbeat: {sub.LastHeartbeat:O}");
                    sb.AppendLine($"  Dependencies: [{string.Join(", ", sub.Dependencies)}]");
                    sb.AppendLine($"  Last Message: {sub.LastMessage}");
                    if (!string.IsNullOrEmpty(sub.LastException))
                    {
                        sb.AppendLine($"  Last Exception: {sub.LastException}");
                    }
                    sb.AppendLine("  Health Transition History:");
                    foreach (var hist in sub.HealthHistory)
                    {
                        sb.AppendLine($"    - {hist}");
                    }
                    sb.AppendLine("--------------------------------------------------");
                }
            }
            else
            {
                sb.AppendLine("IHealthMonitor service is not registered/active.");
            }
            sb.AppendLine("==================================================");

            string content = sb.ToString();
            await File.WriteAllTextAsync(Path.Combine(_reportsDir, "health_summary.txt"), content, ct);
            return content;
        }

        public async Task<string> GenerateRecoveryReportAsync(CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("SAYRA ENTERPRISE WINDOWS CLIENT: RECOVERY REPORT");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
            sb.AppendLine("==================================================");

            var healing = _serviceProvider.GetService<ISelfHealingService>();
            if (healing != null)
            {
                var subsystems = new[] { "Database", "AuditService", "RemoteCommandEngine", "PolicyEngine", "Telemetry", "FleetManager", "AdvertisementEngine", "DownloadManager" };
                foreach (var sub in subsystems)
                {
                    int attempts = healing.GetRecoveryAttemptsCount(sub);
                    sb.AppendLine($"Subsystem: {sub}");
                    sb.AppendLine($"  Self-Healing Recovery Attempts: {attempts}");
                    sb.AppendLine($"  Status: {(attempts == 0 ? "STABLE" : attempts >= 5 ? "RECOVERY_DISABLED_RESTART_LOOP" : "HEALED")}");
                    sb.AppendLine("--------------------------------------------------");
                }
            }
            else
            {
                sb.AppendLine("ISelfHealingService is not registered/active.");
            }
            sb.AppendLine("==================================================");

            string content = sb.ToString();
            await File.WriteAllTextAsync(Path.Combine(_reportsDir, "recovery_report.txt"), content, ct);
            return content;
        }

        public async Task<string> GenerateFailureReportAsync(CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("SAYRA ENTERPRISE WINDOWS CLIENT: FAILURE REPORT");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
            sb.AppendLine("==================================================");

            var monitor = _serviceProvider.GetService<IHealthMonitor>();
            bool hasFailures = false;
            if (monitor != null)
            {
                var detailed = monitor.GetDetailedHealth();
                foreach (var kvp in detailed)
                {
                    var sub = kvp.Value;
                    if (sub.State == SubsystemHealthState.Critical || sub.State == SubsystemHealthState.Offline)
                    {
                        hasFailures = true;
                        sb.AppendLine($"FAILING SUBSYSTEM: {sub.SubsystemName}");
                        sb.AppendLine($"  State: {sub.State}");
                        sb.AppendLine($"  Last Message: {sub.LastMessage}");
                        if (!string.IsNullOrEmpty(sub.LastException))
                        {
                            sb.AppendLine($"  Exception Trace: {sub.LastException}");
                        }
                        sb.AppendLine("--------------------------------------------------");
                    }
                }
            }

            if (!hasFailures)
            {
                sb.AppendLine("No current subsystem failures detected. System is 100% operational.");
            }
            sb.AppendLine("==================================================");

            string content = sb.ToString();
            await File.WriteAllTextAsync(Path.Combine(_reportsDir, "failure_report.txt"), content, ct);
            return content;
        }
    }
}
