using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Performance
{
    /// <summary>
    /// Thread-safe reusable scope implementation for tracking individual performance measurements.
    /// Supports high-precision stopwatch durations, and integrates with the active tracing context.
    /// </summary>
    public sealed class PerformanceMeasurementScope : IPerformanceMeasurement
    {
        private readonly IPerformanceMonitor _parentMonitor;
        private readonly Stopwatch _stopwatch;
        private int _isCompleted; // 0 = active, 1 = completed

        /// <inheritdoc />
        public string OperationName { get; }

        /// <inheritdoc />
        public DateTime StartTime { get; }

        /// <inheritdoc />
        public DateTime? EndTime { get; private set; }

        /// <inheritdoc />
        public TimeSpan Duration { get; private set; } = TimeSpan.Zero;

        /// <inheritdoc />
        public bool IsSuccess { get; private set; } = true;

        /// <inheritdoc />
        public Exception? Exception { get; private set; }

        /// <inheritdoc />
        public string? TraceId { get; }

        /// <inheritdoc />
        public string? CorrelationId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceMeasurementScope"/> class.
        /// </summary>
        public PerformanceMeasurementScope(
            IPerformanceMonitor parentMonitor,
            string operationName,
            string? traceId,
            string? correlationId)
        {
            _parentMonitor = parentMonitor ?? throw new ArgumentNullException(nameof(parentMonitor));
            OperationName = operationName;
            TraceId = traceId;
            CorrelationId = correlationId;
            StartTime = DateTime.UtcNow;
            _stopwatch = Stopwatch.StartNew();
        }

        /// <inheritdoc />
        public void SetSuccess(bool success)
        {
            if (Volatile.Read(ref _isCompleted) == 1) return;
            IsSuccess = success;
        }

        /// <inheritdoc />
        public void CaptureException(Exception exception)
        {
            if (Volatile.Read(ref _isCompleted) == 1) return;
            IsSuccess = false;
            Exception = exception;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Atomically transition from active to completed
            if (Interlocked.CompareExchange(ref _isCompleted, 1, 0) != 0)
            {
                return;
            }

            _stopwatch.Stop();
            EndTime = DateTime.UtcNow;
            Duration = _stopwatch.Elapsed;

            try
            {
                _parentMonitor.RecordMeasurement(this);
            }
            catch
            {
                // Fail-closed/safe: do not let monitoring exceptions crash application disposal
            }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            try
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
            }
        }
    }
}
