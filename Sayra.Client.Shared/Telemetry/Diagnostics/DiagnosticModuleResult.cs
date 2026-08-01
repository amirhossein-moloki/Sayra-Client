using System.Collections.Generic;

namespace Sayra.Client.Shared.Telemetry.Diagnostics
{
    /// <summary>
    /// Holds the evaluation results of an individual diagnostic module.
    /// </summary>
    public class DiagnosticModuleResult
    {
        /// <summary>
        /// Gets the name of the diagnostic module.
        /// </summary>
        public string ModuleName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the evaluated health status of the module.
        /// </summary>
        public DiagnosticHealthStatus Status { get; set; } = DiagnosticHealthStatus.Healthy;

        /// <summary>
        /// Gets list of diagnostic errors.
        /// </summary>
        public List<string> Errors { get; init; } = new();

        /// <summary>
        /// Gets list of diagnostic warnings.
        /// </summary>
        public List<string> Warnings { get; init; } = new();

        /// <summary>
        /// Gets list of diagnostic information messages.
        /// </summary>
        public List<string> Info { get; init; } = new();

        /// <summary>
        /// Gets list of structured findings exposed by the module for recommendation evaluation.
        /// </summary>
        public List<DiagnosticFinding> Findings { get; init; } = new();

        /// <summary>
        /// Gets additional structured metrics or performance statistics.
        /// </summary>
        public Dictionary<string, string> Data { get; init; } = new();
    }
}
