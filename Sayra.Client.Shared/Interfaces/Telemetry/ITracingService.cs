using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Service responsible for distributed tracing across system boundaries.
    /// </summary>
    public interface ITracingService
    {
        /// <summary>
        /// Asynchronously starts a new tracing operation context.
        /// </summary>
        /// <param name="operationName">The name of the operation being executed.</param>
        /// <param name="parentContext">Optional parent trace context to link the new span.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A initialized TraceContext object.</returns>
        Task<TraceContext> StartTraceAsync(string operationName, TraceContext? parentContext = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously ends the specified trace context, setting outcome state and metrics.
        /// </summary>
        /// <param name="context">The active trace context to finalize.</param>
        /// <param name="result">The outcome status of the trace operation.</param>
        /// <param name="exception">The optional error message if the trace failed.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task EndTraceAsync(TraceContext context, TraceResult result, string? exception = null, CancellationToken cancellationToken = default);
    }
}
