using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents a detailed, compiled runtime diagnostics report of the workstation.
    /// </summary>
    public record DiagnosticReport
    {
        /// <summary>
        /// Gets the report generation timestamp.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the identifying name of the workstation machine.
        /// </summary>
        public string MachineId { get; init; } = Environment.MachineName;

        /// <summary>
        /// Gets the high-level summary of the workstation system.
        /// </summary>
        public string MachineSummary { get; init; } = string.Empty;

        /// <summary>
        /// Gets hardware information (CPU, RAM, GPU, storage specifications).
        /// </summary>
        public IReadOnlyDictionary<string, string> Hardware { get; init; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets software inventory details (operating system, installed frameworks, driver versions).
        /// </summary>
        public IReadOnlyList<string> Software { get; init; } = new List<string>();

        /// <summary>
        /// Gets runtime performance diagnostic summaries.
        /// </summary>
        public string PerformanceSummary { get; init; } = string.Empty;

        /// <summary>
        /// Gets a list of diagnostic errors captured in the system.
        /// </summary>
        public IReadOnlyCollection<string> Errors { get; init; } = new List<string>();

        /// <summary>
        /// Gets a list of diagnostic warning events captured in the system.
        /// </summary>
        public IReadOnlyCollection<string> Warnings { get; init; } = new List<string>();

        /// <summary>
        /// Gets the active security hardening and integrity diagnostic status.
        /// </summary>
        public string SecurityStatus { get; init; } = string.Empty;

        /// <summary>
        /// Gets resource usage metrics summary (live CPU, RAM, Disk, GPU percentages).
        /// </summary>
        public string ResourceUsageSummary { get; init; } = string.Empty;

        /// <summary>
        /// Gets individual subsystem health status mappings.
        /// </summary>
        public IReadOnlyDictionary<string, string> SubsystemStatus { get; init; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets a list of recent self-healing and crash recovery events.
        /// </summary>
        public IReadOnlyCollection<string> RecoveryEvents { get; init; } = new List<string>();

        /// <summary>
        /// Gets a collection of system recommendations for optimizing performance or correcting errors.
        /// </summary>
        public IReadOnlyCollection<string> Recommendations { get; init; } = new List<string>();
    }
}
