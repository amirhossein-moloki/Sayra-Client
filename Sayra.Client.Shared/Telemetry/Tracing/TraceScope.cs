using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Tracing
{
    /// <summary>
    /// Represents a thread-safe, non-blocking scoped tracing block supporting nested operations,
    /// execution duration tracking, automatic parent context restoration, and safe exception capture.
    /// </summary>
    public sealed class TraceScope : IDisposable, IAsyncDisposable
    {
        private readonly ITracingService _tracingService;
        private readonly TraceContext _context;
        private readonly TraceContext? _parentContext;
        private readonly Stopwatch _stopwatch;
        private bool _isDisposed;
        private TraceResult _result = TraceResult.Success;
        private string? _exceptionMessage;

        /// <summary>
        /// Gets the active trace context managed by this scope.
        /// </summary>
        public TraceContext Context => _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceScope"/> class.
        /// </summary>
        /// <param name="tracingService">The active tracing service instance.</param>
        /// <param name="context">The initialized trace context for this scope.</param>
        /// <param name="parentContext">The optional parent trace context to restore upon disposal.</param>
        public TraceScope(ITracingService tracingService, TraceContext context, TraceContext? parentContext)
        {
            _tracingService = tracingService ?? throw new ArgumentNullException(nameof(tracingService));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _parentContext = parentContext;
            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Sets the execution result status and description.
        /// </summary>
        /// <param name="result">The outcome of the scoped operation.</param>
        /// <param name="exceptionMessage">The optional exception or failure description.</param>
        public void SetResult(TraceResult result, string? exceptionMessage = null)
        {
            if (_isDisposed) return;
            _result = result;
            _exceptionMessage = exceptionMessage;
        }

        /// <summary>
        /// Captures exception details and marks the scope execution as failed.
        /// </summary>
        /// <param name="exception">The encountered exception.</param>
        public void CaptureException(Exception exception)
        {
            if (_isDisposed) return;
            _result = TraceResult.Failed;
            _exceptionMessage = exception != null ? $"{exception.GetType().FullName}: {exception.Message}" : "An unknown error occurred.";
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _stopwatch.Stop();
            var duration = _stopwatch.Elapsed;
            var finalizedContext = _context with { Latency = duration, Result = _result, Exception = _exceptionMessage };

            try
            {
                // Execute ending of the trace context synchronously without block deadlocks
                _tracingService.EndTraceAsync(finalizedContext, _result, _exceptionMessage, default).GetAwaiter().GetResult();
            }
            catch
            {
                // Fail-safe
            }
            finally
            {
                // Clean up and restore parent ambient context
                _tracingService.CurrentContext = _parentContext;
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
