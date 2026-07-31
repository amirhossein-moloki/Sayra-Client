using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Telemetry.Results
{
    /// <summary>
    /// Represents the result of a batch metrics collection cycle execution.
    /// </summary>
    public record CollectionResult
    {
        /// <summary>Gets a value indicating whether the collection batch cycle was completed successfully.</summary>
        public bool IsSuccess { get; init; }

        /// <summary>Gets the collection of recorded metrics, if successful.</summary>
        public IReadOnlyCollection<MetricPoint> Metrics { get; init; } = new List<MetricPoint>();

        /// <summary>Gets any details or logging error message.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Gets the timestamp of the operation.</summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>Creates a successful collection result.</summary>
        public static CollectionResult Success(IReadOnlyCollection<MetricPoint> metrics, string message = "") => new() { IsSuccess = true, Metrics = metrics, Message = message };

        /// <summary>Creates a failed collection result.</summary>
        public static CollectionResult Failure(string error) => new() { IsSuccess = false, Message = error };
    }
}
