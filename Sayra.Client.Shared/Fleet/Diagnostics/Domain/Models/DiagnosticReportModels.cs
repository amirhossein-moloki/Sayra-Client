using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Diagnostics.Domain.Models
{
    /// <summary>
    /// Represents the current status of a diagnostics execution session.
    /// </summary>
    public enum DiagnosticExecutionStatus
    {
        /// <summary>
        /// Execution is pending and has not started yet.
        /// </summary>
        Pending,

        /// <summary>
        /// Execution is currently running.
        /// </summary>
        Running,

        /// <summary>
        /// Execution completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// Execution failed due to errors.
        /// </summary>
        Failed,

        /// <summary>
        /// Execution was cancelled by the administrator or due to timeout.
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// Represents a section within a diagnostic report, containing metrics and findings.
    /// </summary>
    public record DiagnosticSection
    {
        /// <summary>
        /// Gets the name of the section.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the list of metrics evaluated in this section.
        /// </summary>
        public List<DiagnosticMetric> Metrics { get; init; } = new();

        /// <summary>
        /// Gets the list of findings discovered in this section.
        /// </summary>
        public List<DiagnosticFinding> Findings { get; init; } = new();
    }

    /// <summary>
    /// Represents a individual diagnostic metric reading.
    /// </summary>
    public record DiagnosticMetric
    {
        /// <summary>
        /// Gets the name of the metric.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the value of the metric as a string.
        /// </summary>
        public string Value { get; init; } = string.Empty;

        /// <summary>
        /// Gets the unit of measurement (e.g. "%", "GB", "ms", or empty).
        /// </summary>
        public string Unit { get; init; } = string.Empty;

        /// <summary>
        /// Gets the status of this metric (e.g. "Normal", "Warning", "Critical").
        /// </summary>
        public string Status { get; init; } = "Normal";
    }

    /// <summary>
    /// Represents a finding discovered during the diagnostics pass.
    /// </summary>
    public record DiagnosticFinding
    {
        /// <summary>
        /// Gets the unique identifier for the finding.
        /// </summary>
        public string FindingId { get; init; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets the name of the diagnostic rule triggered.
        /// </summary>
        public string RuleName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the severity of this finding (e.g. "Information", "Warning", "Critical", "Emergency").
        /// </summary>
        public string Severity { get; init; } = "Information";

        /// <summary>
        /// Gets the description detailing what anomaly or condition was found.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets the category/subsystem of the finding.
        /// </summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>
        /// Gets the list of generated recommendations to address this finding.
        /// </summary>
        public List<DiagnosticRecommendation> Recommendations { get; init; } = new();
    }

    /// <summary>
    /// Represents an actionable recommendation to resolve an issue discovered.
    /// </summary>
    public record DiagnosticRecommendation
    {
        /// <summary>
        /// Gets the unique identifier of the recommendation.
        /// </summary>
        public string RecommendationId { get; init; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets the descriptive summary of the recommendation.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Gets the specific actionable step that can be performed.
        /// </summary>
        public string ActionableStep { get; init; } = string.Empty;

        /// <summary>
        /// Gets the priority of the recommendation (e.g. "Low", "Medium", "High").
        /// </summary>
        public string Priority { get; init; } = "Low";
    }

    /// <summary>
    /// Represents metadata associated with the workstation diagnostic collection run.
    /// </summary>
    public record DiagnosticMetadata
    {
        /// <summary>
        /// Gets the workstation machine identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the Operating System version.
        /// </summary>
        public string OSVersion { get; init; } = string.Empty;

        /// <summary>
        /// Gets the active SAYRA agent version.
        /// </summary>
        public string AgentVersion { get; init; } = "1.0.0";

        /// <summary>
        /// Gets the tracking correlation ID.
        /// </summary>
        public string CorrelationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the timestamp of when this metadata was generated.
        /// </summary>
        public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents the immutable final result of a complete remote diagnostics execution.
    /// </summary>
    public record DiagnosticResult
    {
        /// <summary>
        /// Gets the unique diagnostics session tracking identifier.
        /// </summary>
        public string DiagnosticId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the target workstation machine identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the computed overall health score (0.0 to 100.0) for this diagnostics pass.
        /// </summary>
        public double HealthScore { get; init; } = 100.0;

        /// <summary>
        /// Gets the overall health classification status (e.g. "Healthy", "Warning", "Critical").
        /// </summary>
        public string OverallStatus { get; init; } = "Healthy";

        /// <summary>
        /// Gets the collection of generated diagnostic reports.
        /// </summary>
        public List<DiagnosticReport> Reports { get; init; } = new();

        /// <summary>
        /// Gets the aggregated list of findings across all reports.
        /// </summary>
        public List<DiagnosticFinding> Findings { get; init; } = new();

        /// <summary>
        /// Gets the consolidated list of recommendations to resolve issues.
        /// </summary>
        public List<DiagnosticRecommendation> Recommendations { get; init; } = new();

        /// <summary>
        /// Gets the local path where the compressed diagnostic package was generated, if any.
        /// </summary>
        public string PackagePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets whether the diagnostics session succeeded completely.
        /// </summary>
        public bool IsSuccess { get; init; }

        /// <summary>
        /// Gets any error message in case of failure.
        /// </summary>
        public string ErrorMessage { get; init; } = string.Empty;

        /// <summary>
        /// Gets the session start timestamp.
        /// </summary>
        public DateTime StartedAtUtc { get; init; }

        /// <summary>
        /// Gets the session end/completion timestamp.
        /// </summary>
        public DateTime EndedAtUtc { get; init; }
    }
}
