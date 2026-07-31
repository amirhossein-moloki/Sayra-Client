using System;

namespace Sayra.Client.Shared.Models.Telemetry.Results
{
    /// <summary>
    /// Represents the result of a dashboard snapshot retrieval or subscription stream operation.
    /// </summary>
    public record DashboardResult
    {
        /// <summary>Gets a value indicating whether the dashboard operation was successful.</summary>
        public bool IsSuccess { get; init; }

        /// <summary>Gets the latest dashboard snapshot state, if successful.</summary>
        public DashboardSnapshot? Snapshot { get; init; }

        /// <summary>Gets any details or logging error message.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Gets the timestamp of the operation.</summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>Creates a successful dashboard result.</summary>
        public static DashboardResult Success(DashboardSnapshot snapshot, string message = "") => new() { IsSuccess = true, Snapshot = snapshot, Message = message };

        /// <summary>Creates a failed dashboard result.</summary>
        public static DashboardResult Failure(string error) => new() { IsSuccess = false, Message = error };
    }
}
