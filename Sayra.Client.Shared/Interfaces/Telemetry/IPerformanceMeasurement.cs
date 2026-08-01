using System;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Represents a reusable, thread-safe measurement scope that tracks operation details,
    /// timestamps, execution duration, outcome status, and tracing context integration.
    /// </summary>
    public interface IPerformanceMeasurement : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Gets the name of the operation being measured.
        /// </summary>
        string OperationName { get; }

        /// <summary>
        /// Gets the UTC start timestamp of the measurement.
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// Gets the UTC end timestamp of the measurement, or null if still running.
        /// </summary>
        DateTime? EndTime { get; }

        /// <summary>
        /// Gets the total measured execution duration.
        /// </summary>
        TimeSpan Duration { get; }

        /// <summary>
        /// Gets a value indicating whether the measured operation was successful.
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// Gets the exception encountered during the operation, if any.
        /// </summary>
        Exception? Exception { get; }

        /// <summary>
        /// Gets the Trace ID associated with this measurement.
        /// </summary>
        string? TraceId { get; }

        /// <summary>
        /// Gets the Correlation ID associated with this measurement.
        /// </summary>
        string? CorrelationId { get; }

        /// <summary>
        /// Sets whether the measured operation succeeded or failed.
        /// </summary>
        /// <param name="success">True if successful; false otherwise.</param>
        void SetSuccess(bool success);

        /// <summary>
        /// Captures exception details and marks the measurement as failed.
        /// </summary>
        /// <param name="exception">The encountered exception.</param>
        void CaptureException(Exception exception);
    }
}
