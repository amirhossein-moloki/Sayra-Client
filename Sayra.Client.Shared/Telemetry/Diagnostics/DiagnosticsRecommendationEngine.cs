using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Telemetry.Diagnostics
{
    /// <summary>
    /// Evaluates diagnostic findings against defined system rules and translates them to recommendations.
    /// </summary>
    public class DiagnosticsRecommendationEngine : IDiagnosticsRecommendationEngine
    {
        public IEnumerable<DiagnosticRecommendation> Evaluate(IEnumerable<DiagnosticFinding> findings)
        {
            var recommendations = new List<DiagnosticRecommendation>();

            if (findings == null) return recommendations;

            foreach (var finding in findings)
            {
                if (!finding.IsAnomaly) continue;

                switch (finding.Key)
                {
                    case "CpuUsageLimitExceeded":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Hardware",
                            Description = $"High CPU utilization detected ({finding.Value}), causing potential UI stutters or game frame rate drops.",
                            RecommendedAction = "Identify and terminate background non-game processes consuming high CPU or upgrade CPU.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "LowAvailableRam":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Hardware",
                            Description = $"Critical low memory condition ({finding.Value} left). The system is at risk of OutOfMemoryException or heavy virtual memory paging thrashing.",
                            RecommendedAction = "Close heavy secondary applications, clean up browser processes, or install additional physical RAM.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "LowFreeSpace":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "Hardware",
                            Description = $"Low primary storage disk space ({finding.Value} remaining), which might restrict game updates or system temp file staging.",
                            RecommendedAction = "Run Disk Cleanup, clear download caches, or delete unused log archives.",
                            Priority = "Medium",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "HighHardwareTemp":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "Hardware",
                            Description = $"Elevated hardware component temperature detected: {finding.Value}.",
                            RecommendedAction = "Ensure workstation has adequate airflow, verify cooling fans are operational, or clean dust from heatsinks.",
                            Priority = "Medium",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "OutdatedOS":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "OS",
                            Description = "32-bit architecture limits available RAM addressing space to 4GB.",
                            RecommendedAction = "Reinstall the system using Windows 64-bit to leverage full physical memory.",
                            Priority = "Medium",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "StarvedThreadPool":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Runtime",
                            Description = "High thread pool exhaustion indicates synchronous blocking operations or potential lock contention.",
                            RecommendedAction = "Refactor blocking I/O calls to use async-await patterns, optimize lock scopes, or increase ThreadPool limits.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "HighCpuHeap":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "Runtime",
                            Description = $"Process GC heap size is elevated: {finding.Value}.",
                            RecommendedAction = "Invoke explicit GC.Collect() or investigate memory leaks (e.g. event handlers, static collections).",
                            Priority = "Medium",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "UnhealthyWorkers":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Runtime",
                            Description = "ServiceHealthMonitor reported background worker errors or heartbeat failures.",
                            RecommendedAction = "Check application logs for unhandled worker service crashes and trigger automatic worker restart.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "ServerConnectionLost":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Network",
                            Description = "The central server endpoint is unreachable or timing out consistently.",
                            RecommendedAction = "Verify regional router settings, check network interface cables, check server service status, or trace connection routing.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "HighNetworkLatency":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "Network",
                            Description = $"Extreme connection latency ({finding.Value}) may degrade administrative remote operations, synchronization, and local game launches.",
                            RecommendedAction = "Limit network-intensive downloads, review bandwidth allocation/shaping policies, or check for local network congestion.",
                            Priority = "Medium",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "HighPacketLoss":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "Network",
                            Description = $"High packet failure rate ({finding.Value}) is causing TCP retries, reducing overall bandwidth and connection stability.",
                            RecommendedAction = "Check for bad network cables, update network adapter drivers, or contact internet service provider for line noise analysis.",
                            Priority = "Medium",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "LocalDbOffline":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Database",
                            Description = "The SQLCipher local database could not be initialized or opened due to a master key mismatch or disk-level corruption.",
                            RecommendedAction = "Run Database Recovery Service to recreate the schema, verify DPAPI key envelope integrity, or restore from workstation backup.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "SlowDatabaseQueries":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "Database",
                            Description = $"Slow database query execution times ({finding.Value}) are degrading workstation performance and UI state synchronization.",
                            RecommendedAction = "Verify SQLite index coverage, run standard VACUUM and ANALYZE operations, or check storage IO bottlenecks.",
                            Priority = "Medium",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "LocalDbCorruption":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Database",
                            Description = $"Database queries are failing frequently ({finding.Value}), indicating locked files, schema drift, or physical disk failures.",
                            RecommendedAction = "Restart the application host to clear connection pool locks, and perform SQLCipher integrity PRAGMA check.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "LowWriteAccess":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Storage",
                            Description = "The application host does not have permission to write to its database directory or disk is write-protected.",
                            RecommendedAction = "Grant Full Control NTFS/folder permissions to the service user, or check disk physical health and hardware lock switches.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "TempFolderCongested":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "Storage",
                            Description = $"Excessive temporary file congestion ({finding.Value}) consumes physical drive sectors and may slow down overall system performance.",
                            RecommendedAction = "Run standard system disk cleanup or purge old application installer logs and crash mini-dumps.",
                            Priority = "Low",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "ConfigSignatureTampered":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Security",
                            Description = "The workstation's digital signature for the local configuration does not match the public key verification.",
                            RecommendedAction = "Immediately synchronize with the central admin policy console to overwrite and sign local config files.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "DatabaseIntegrityTampered":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Security",
                            Description = "Tampering or corruption detected in SQLCipher secure local databases.",
                            RecommendedAction = "Run database integrity repair routines, ensure correct master key generation, or restore state from backup.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "AuthenticodeValidationFailed":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "Security",
                            Description = "One or more DLLs or executables have missing or invalid digital signatures, indicating unauthorized updates or code modification.",
                            RecommendedAction = "Run Update Platform Part 5 Validator or trigger full software package repairs to re-deploy signed binaries.",
                            Priority = "Medium",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "PluginCrashed":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "Plugins",
                            Description = "An active workstation plugin crashed, which could disrupt secondary telemetry or billing overlay integrations.",
                            RecommendedAction = "Examine plugin logs, verify DLL target framework compatibility (must compile against .NET 8), or disable the faulty plugin.",
                            Priority = "Medium",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "ConfigValidationFailed":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Configuration",
                            Description = "One or more strongly bound configuration sections violate built-in schema validation constraints.",
                            RecommendedAction = "Verify appsettings.json options formatting, ensure values fall within safe bounds, and trigger configuration reload.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "IpcServerOffline":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "IPC",
                            Description = "The local Named Pipe server is completely unresponsive or refused connections, which breaks WPF UI communication.",
                            RecommendedAction = "Restart the core background workstation service and verify named pipe access control list (DACL) configuration.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "ElevatedIpcLatency":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "IPC",
                            Description = $"IPC latency ({finding.Value}) exceeds normal local loop bounds (usually < 5ms). This causes sluggish desktop dashboard UI transitions.",
                            RecommendedAction = "Verify host machine CPU load is normal and check for thread pool congestion blocking pipe handlers.",
                            Priority = "Medium",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "SyncServiceOffline":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Synchronization",
                            Description = "Local database changes, logs, and billing sessions are failing to sync with the central server.",
                            RecommendedAction = "Verify TCP transport security / certificate pinning parameters, check server connectivity, or trigger force sync.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "NotificationDbExcessive":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "Notifications",
                            Description = $"Excessive size in the notification repository ({finding.Value}) can degrade local query times and UI load latency.",
                            RecommendedAction = "Run notifications database retention pruning or execute SQL VACUUM command.",
                            Priority = "Low",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "MirrorsOffline":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Critical",
                            Category = "Downloads",
                            Description = "The download subsystem failed to locate or ping any operational server mirror for game updates and packages.",
                            RecommendedAction = "Verify regional DNS cache settings, make sure TCP port 443 is not blocked by Windows Firewall, or update mirror list.",
                            Priority = "High",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;

                    case "StagingDirectoryCongested":
                        recommendations.Add(new DiagnosticRecommendation
                        {
                            Severity = "Warning",
                            Category = "Updates",
                            Description = $"Staging folder size is elevated ({finding.Value}), which can consume precious SSD write/read sectors and system space.",
                            RecommendedAction = "Run post-installation cleanups or force the Update Platform installer engine to purge corrupted package chunks.",
                            Priority = "Medium",
                            AffectedSubsystem = finding.Subsystem
                        });
                        break;
                }
            }

            return recommendations;
        }
    }
}
