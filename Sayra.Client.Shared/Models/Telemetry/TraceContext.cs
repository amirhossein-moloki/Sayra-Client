using System;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.ValueObjects;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents the distributed context of an execution operation trace.
    /// </summary>
    public record TraceContext
    {
        /// <summary>
        /// Gets the globally unique trace identifier.
        /// </summary>
        public TraceId TraceId { get; init; } = new();

        /// <summary>
        /// Gets the logical correlation identifier linking related activities.
        /// </summary>
        public CorrelationId CorrelationId { get; init; } = new();

        /// <summary>
        /// Gets the unique identifier of the specific operation span.
        /// </summary>
        public string OperationId { get; init; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets the identifier of the parent operation span, if any.
        /// </summary>
        public string? ParentOperationId { get; init; }

        /// <summary>
        /// Gets the identifier of the workstation machine where this execution occurred.
        /// </summary>
        public string MachineId { get; init; } = Environment.MachineName;

        /// <summary>
        /// Gets the identifier of the current active user session.
        /// </summary>
        public string? SessionId { get; init; }

        /// <summary>
        /// Gets the identifier of the logged-on user.
        /// </summary>
        public string? UserId { get; init; }

        /// <summary>
        /// Gets the identifier of the gaming center.
        /// </summary>
        public string? CenterId { get; init; }

        /// <summary>
        /// Gets the total processing latency of the traced operation.
        /// </summary>
        public TimeSpan Latency { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the execution result state of the traced operation.
        /// </summary>
        public TraceResult Result { get; init; } = TraceResult.Success;

        /// <summary>
        /// Gets the exception details if the operation resulted in a failure.
        /// </summary>
        public string? Exception { get; init; }
    }
}
