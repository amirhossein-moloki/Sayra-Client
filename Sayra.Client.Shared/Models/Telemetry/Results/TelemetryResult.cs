using System;

namespace Sayra.Client.Shared.Models.Telemetry.Results
{
    /// <summary>
    /// Represents the result of a telemetry record tracking or dispatching operation.
    /// </summary>
    public record TelemetryResult
    {
        /// <summary>Gets a value indicating whether the telemetry operation completed successfully.</summary>
        public bool IsSuccess { get; init; }

        /// <summary>Gets any details or logging error message.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Gets the timestamp of the operation.</summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>Creates a successful telemetry result.</summary>
        public static TelemetryResult Success(string message = "") => new() { IsSuccess = true, Message = message };

        /// <summary>Creates a failed telemetry result.</summary>
        public static TelemetryResult Failure(string error) => new() { IsSuccess = false, Message = error };
    }
}
