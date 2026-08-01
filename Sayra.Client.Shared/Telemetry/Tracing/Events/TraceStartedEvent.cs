using System;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Tracing.Events
{
    /// <summary>
    /// Event dispatched when a tracing operation or span is started.
    /// </summary>
    public record TraceStartedEvent(TraceContext Context, string OperationName, DateTime Timestamp);
}
