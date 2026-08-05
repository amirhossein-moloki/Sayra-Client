using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Interfaces.Fleet
{
    /// <summary>
    /// Thread-safe storage repository for persisting and recovering bulk operations, results, failures, and execution progress.
    /// </summary>
    public interface IBulkOperationRepository
    {
        /// <summary>
        /// Saves or updates a bulk operation's main definition and status.
        /// </summary>
        Task SaveOperationAsync(BulkOperation operation, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a bulk operation by its unique ID.
        /// </summary>
        Task<BulkOperation?> GetOperationAsync(string bulkOperationId, CancellationToken ct = default);

        /// <summary>
        /// Gets all bulk operations currently tracked.
        /// </summary>
        Task<IReadOnlyList<BulkOperation>> GetAllOperationsAsync(CancellationToken ct = default);

        /// <summary>
        /// Saves the target routing criteria associated with a bulk operation.
        /// </summary>
        Task SaveTargetsAsync(string bulkOperationId, IEnumerable<BulkOperationTarget> targets, CancellationToken ct = default);

        /// <summary>
        /// Retrieves targets registered for a specific bulk operation.
        /// </summary>
        Task<IReadOnlyList<BulkOperationTarget>> GetTargetsAsync(string bulkOperationId, CancellationToken ct = default);

        /// <summary>
        /// Saves or updates the final result summary of a bulk operation.
        /// </summary>
        Task SaveResultAsync(BulkOperationResult result, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the result summary for a bulk operation.
        /// </summary>
        Task<BulkOperationResult?> GetResultAsync(string bulkOperationId, CancellationToken ct = default);

        /// <summary>
        /// Saves a single workstation execution failure.
        /// </summary>
        Task SaveFailureAsync(string bulkOperationId, BulkOperationFailure failure, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all workstation execution failures for a specific bulk operation.
        /// </summary>
        Task<IReadOnlyList<BulkOperationFailure>> GetFailuresAsync(string bulkOperationId, CancellationToken ct = default);

        /// <summary>
        /// Appends or updates the tracking progress of an active bulk operation.
        /// </summary>
        Task SaveProgressAsync(string bulkOperationId, BulkOperationProgress progress, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the current progress tracking metrics for a bulk operation.
        /// </summary>
        Task<BulkOperationProgress?> GetProgressAsync(string bulkOperationId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves chronological progress history/snapshots recorded for an operation.
        /// </summary>
        Task<IReadOnlyList<BulkOperationProgress>> GetProgressHistoryAsync(string bulkOperationId, CancellationToken ct = default);

        /// <summary>
        /// Saves the execution state of an individual target machine.
        /// </summary>
        Task SaveExecutionStateAsync(string bulkOperationId, BulkOperationExecution execution, CancellationToken ct = default);

        /// <summary>
        /// Gets execution states of all target machines for a bulk operation.
        /// </summary>
        Task<IReadOnlyList<BulkOperationExecution>> GetExecutionsAsync(string bulkOperationId, CancellationToken ct = default);

        /// <summary>
        /// Saves or updates the execution policy for a bulk operation.
        /// </summary>
        Task SavePolicyAsync(string bulkOperationId, BulkOperationPolicy policy, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the execution policy for a bulk operation.
        /// </summary>
        Task<BulkOperationPolicy?> GetPolicyAsync(string bulkOperationId, CancellationToken ct = default);
    }
}
