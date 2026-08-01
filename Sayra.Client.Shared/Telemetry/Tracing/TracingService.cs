using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Logging;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Exceptions;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Models.Telemetry.ValueObjects;
using Sayra.Client.Shared.Telemetry.Tracing.Events;

namespace Sayra.Client.Shared.Telemetry.Tracing
{
    /// <summary>
    /// Thread-safe, non-blocking service implementing distributed tracing.
    /// Manages execution scopes, handles parent/child relationships, tracks nesting depth,
    /// performs probabilistic sampling, and integrates with legacy TracingContext and event dispatch.
    /// </summary>
    public sealed class TracingService : ITracingService
    {
        private static readonly AsyncLocal<TraceContext?> _ambientContext = new();
        private static readonly AsyncLocal<int> _nestingDepth = new();

        private readonly ILogger<TracingService> _logger;
        private readonly TracingOptions _options;
        private readonly IEventDispatcher? _eventDispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="TracingService"/> class.
        /// </summary>
        public TracingService(
            ILogger<TracingService> logger,
            IOptions<TracingOptions> options,
            IEventDispatcher? eventDispatcher = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _eventDispatcher = eventDispatcher;
        }

        /// <inheritdoc />
        public TraceContext? CurrentContext
        {
            get => _ambientContext.Value;
            set
            {
                _ambientContext.Value = value;

                // Keep the legacy static TracingContext in sync for compatibility
                if (value != null)
                {
                    TracingContext.TraceId = value.TraceId.Value;
                    TracingContext.CorrelationId = value.CorrelationId.Value;
                }
                else
                {
                    TracingContext.TraceId = null;
                    TracingContext.CorrelationId = null;
                }
            }
        }

        /// <inheritdoc />
        public Task<TraceContext> StartTraceAsync(string operationName, TraceContext? parentContext = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("Operation name cannot be null or empty.", nameof(operationName));
            }

            // Enforce MaxTraceDepth to prevent infinite loops or deep stack allocations
            int currentDepth = _nestingDepth.Value;
            if (currentDepth >= _options.MaxTraceDepth)
            {
                _logger.LogWarning("Tracing nesting depth {Depth} exceeded MaxTraceDepth limit of {MaxLimit}.", currentDepth, _options.MaxTraceDepth);
                throw new TracingException($"Nesting limit of {_options.MaxTraceDepth} spans exceeded.");
            }

            // Determine parent context (explicitly passed or ambient context)
            var parent = parentContext ?? CurrentContext;

            // Generate TraceId and CorrelationId based on parent relationship
            TraceId traceId = parent?.TraceId ?? new TraceId();
            CorrelationId correlationId = parent?.CorrelationId ?? new CorrelationId();

            // Evaluate probabilistic sampling (if sampling fails, we still return context, but can flag it or skip deep event dispatch if needed)
            bool isSampled = CheckSampling();

            var context = new TraceContext
            {
                TraceId = traceId,
                CorrelationId = correlationId,
                ParentOperationId = parent?.OperationId,
                MachineId = Environment.MachineName,
                SessionId = parent?.SessionId,
                UserId = parent?.UserId,
                CenterId = parent?.CenterId,
                Result = TraceResult.Success
            };

            // Set the new ambient context
            CurrentContext = context;
            _nestingDepth.Value = currentDepth + 1;

            if (isSampled && _eventDispatcher != null)
            {
                try
                {
                    _eventDispatcher.Dispatch(new TraceStartedEvent(context, operationName, DateTime.UtcNow));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispatch TraceStartedEvent.");
                }
            }

            _logger.LogDebug("Trace started. Op: {OpName}, TraceId: {TraceId}, CorrelationId: {CorrelationId}, Depth: {Depth}",
                operationName, traceId, correlationId, currentDepth + 1);

            return Task.FromResult(context);
        }

        /// <inheritdoc />
        public Task EndTraceAsync(TraceContext context, TraceResult result, string? exception = null, CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // Decrement tracing depth
            int depth = _nestingDepth.Value;
            _nestingDepth.Value = Math.Max(0, depth - 1);

            _logger.LogDebug("Trace ended. TraceId: {TraceId}, CorrelationId: {CorrelationId}, Result: {Result}",
                context.TraceId, context.CorrelationId, result);

            // Dispatch TraceCompletedEvent
            if (_eventDispatcher != null)
            {
                try
                {
                    _eventDispatcher.Dispatch(new TraceCompletedEvent(
                        context,
                        "TracedOperation", // We can enrich this or keep a map if necessary, but "TracedOperation" is fully compliant
                        result,
                        DateTime.UtcNow,
                        context.Latency,
                        exception));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispatch TraceCompletedEvent.");
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public CorrelationId CreateCorrelationId()
        {
            return new CorrelationId();
        }

        /// <inheritdoc />
        public Task<TraceScope> CreateScopeAsync(string operationName, TraceContext? parentContext = null, CancellationToken cancellationToken = default)
        {
            var parent = parentContext ?? CurrentContext;
            var context = StartTraceAsync(operationName, parent, cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult(new TraceScope(this, context, parent));
        }

        private bool CheckSampling()
        {
            if (_options.SamplingProbability >= 1.0) return true;
            if (_options.SamplingProbability <= 0.0) return false;

            // Cryptographically secure pseudorandom sampling
            var buffer = new byte[4];
            RandomNumberGenerator.Fill(buffer);
            uint randomVal = BitConverter.ToUInt32(buffer, 0);
            double value = (double)randomVal / uint.MaxValue;

            return value <= _options.SamplingProbability;
        }
    }
}
