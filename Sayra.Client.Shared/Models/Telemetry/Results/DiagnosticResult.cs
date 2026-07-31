using System;

namespace Sayra.Client.Shared.Models.Telemetry.Results
{
    /// <summary>
    /// Represents the result of a workstation diagnostic compilation or evaluation.
    /// </summary>
    public record DiagnosticResult
    {
        /// <summary>Gets a value indicating whether the diagnostic compilation was fully successful.</summary>
        public bool IsSuccess { get; init; }

        /// <summary>Gets the diagnostic compiled report payload, if successful.</summary>
        public DiagnosticReport? Report { get; init; }

        /// <summary>Gets any details or diagnostic summary message.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Gets the timestamp of the evaluation.</summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>Creates a successful diagnostic result.</summary>
        public static DiagnosticResult Success(DiagnosticReport report, string message = "") => new() { IsSuccess = true, Report = report, Message = message };

        /// <summary>Creates a failed diagnostic result.</summary>
        public static DiagnosticResult Failure(string error) => new() { IsSuccess = false, Message = error };
    }
}
