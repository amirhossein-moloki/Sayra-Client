using System;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Models.Telemetry.Results
{
    /// <summary>
    /// Represents the evaluated result of a subsystem health validation check.
    /// </summary>
    public record HealthCheckResult
    {
        /// <summary>Gets the name of the evaluated subsystem.</summary>
        public string Subsystem { get; init; } = string.Empty;

        /// <summary>Gets the evaluated health diagnostic status of the subsystem.</summary>
        public DiagnosticStatus Status { get; init; } = DiagnosticStatus.Healthy;

        /// <summary>Gets details describing any errors, warnings, or healthy status logs.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Gets the exact timestamp of the evaluation.</summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>Creates a successful/healthy check result.</summary>
        public static HealthCheckResult Healthy(string subsystem, string message = "") => new() { Subsystem = subsystem, Status = DiagnosticStatus.Healthy, Message = message };

        /// <summary>Creates a warning check result.</summary>
        public static HealthCheckResult Warning(string subsystem, string message) => new() { Subsystem = subsystem, Status = DiagnosticStatus.Warning, Message = message };

        /// <summary>Creates a critical/failed check result.</summary>
        public static HealthCheckResult Critical(string subsystem, string message) => new() { Subsystem = subsystem, Status = DiagnosticStatus.Critical, Message = message };
    }
}
