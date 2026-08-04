using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Fleet.Diagnostics.Domain.Models;

namespace Sayra.Client.Shared.Fleet.Diagnostics.Interfaces
{
    /// <summary>
    /// Execution context passed to diagnostic collectors during the diagnostics run.
    /// </summary>
    public class DiagnosticsExecutionContext
    {
        /// <summary>
        /// Gets the unique identifier for the current diagnostics execution.
        /// </summary>
        public string DiagnosticId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the target workstation machine identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the tracking correlation ID.
        /// </summary>
        public string CorrelationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the active operator identifier.
        /// </summary>
        public string OperatorId { get; init; } = string.Empty;
    }

    /// <summary>
    /// Base interface for all pluggable diagnostic collectors.
    /// </summary>
    public interface IDiagnosticCollector
    {
        /// <summary>
        /// Gets the type of diagnostic report this collector compiles.
        /// </summary>
        DiagnosticReportType ReportType { get; }

        /// <summary>
        /// Asynchronously executes collection and compiles a structured report.
        /// </summary>
        Task<DiagnosticReport> CollectAsync(DiagnosticsExecutionContext context, CancellationToken ct = default);
    }

    /// <summary>
    /// Collector compiling current subsystem statuses, warnings, failures, and overall machine health.
    /// </summary>
    public interface IHealthDiagnosticCollector : IDiagnosticCollector { }

    /// <summary>
    /// Collector compiling current CPU, memory, GPU, disk, and process performance statistics.
    /// </summary>
    public interface IPerformanceDiagnosticCollector : IDiagnosticCollector { }

    /// <summary>
    /// Collector compiling recent application crashes, exceptions, and Windows Event Logs.
    /// </summary>
    public interface ICrashDiagnosticCollector : IDiagnosticCollector { }

    /// <summary>
    /// Collector compiling workstation configurations, settings, active registry, and environments.
    /// </summary>
    public interface IConfigurationDiagnosticCollector : IDiagnosticCollector { }

    /// <summary>
    /// Collector compiling local system security statuses, permissions, firewalls, and security events.
    /// </summary>
    public interface ISecurityDiagnosticCollector : IDiagnosticCollector { }

    /// <summary>
    /// Collector compiling local SQLCipher database status, size, schema versions, and integrity checks.
    /// </summary>
    public interface IDatabaseDiagnosticCollector : IDiagnosticCollector { }

    /// <summary>
    /// Collector compiling installed SAYRA client plugins, versions, and compatibility profiles.
    /// </summary>
    public interface IPluginDiagnosticCollector : IDiagnosticCollector { }

    /// <summary>
    /// Collector compiling local network adapter, IP addresses, latency, packet loss, and connectivity.
    /// </summary>
    public interface INetworkDiagnosticCollector : IDiagnosticCollector { }

    /// <summary>
    /// Collector compiling disk health status, SMART metrics, and partitions capacity layouts.
    /// </summary>
    public interface IStorageDiagnosticCollector : IDiagnosticCollector { }
}
