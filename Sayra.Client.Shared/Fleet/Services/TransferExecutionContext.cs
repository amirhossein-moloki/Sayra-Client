using System;
using System.Threading;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Represents the execution context for a transfer, carrying correlation IDs and progress.
    /// </summary>
    public class TransferExecutionContext
    {
        /// <summary>
        /// Gets the trace identifier.
        /// </summary>
        public string TraceId { get; init; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets the correlation identifier.
        /// </summary>
        public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets the target machine identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the transfer identifier.
        /// </summary>
        public string TransferId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the name of the file being processed.
        /// </summary>
        public string FileName { get; init; } = string.Empty;

        /// <summary>
        /// Gets when the execution started in UTC.
        /// </summary>
        public DateTime StartedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets total bytes successfully transferred.
        /// </summary>
        public long TransferredBytes { get; set; }

        /// <summary>
        /// Gets or sets the overall execution result string.
        /// </summary>
        public string Result { get; set; } = "Unknown";

        /// <summary>
        /// Gets or sets any failure description if the transfer encountered errors.
        /// </summary>
        public string? FailureReason { get; set; }

        /// <summary>
        /// Gets the cancellation token for the execution.
        /// </summary>
        public CancellationToken Token { get; init; } = CancellationToken.None;
    }
}
