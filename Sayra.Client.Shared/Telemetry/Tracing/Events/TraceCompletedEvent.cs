using System;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Tracing.Events
{
    /// <summary>
    /// Event dispatched when a tracing operation or span is completed.
    /// </summary>
    public record TraceCompletedEvent(
        TraceContext Context,
        string OperationName,
        TraceResult Result,
        DateTime Timestamp,
        TimeSpan Duration,
        string? Exception = null);
}
