using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Repository interface for saving and querying system/operation performance snapshots.
    /// </summary>
    public interface IPerformanceSnapshotRepository
    {
        /// <summary>
        /// Inserts a single performance snapshot.
        /// </summary>
        Task InsertAsync(PerformanceSnapshot snapshot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts a batch of performance snapshots.
        /// </summary>
        Task BatchInsertAsync(IEnumerable<PerformanceSnapshot> snapshots, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries performance snapshots using filtering parameters.
        /// </summary>
        Task<IReadOnlyCollection<PerformanceSnapshot>> QueryAsync(
            DateTime? start = null,
            DateTime? end = null,
            string? subsystem = null,
            string? machineId = null,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes performance snapshots older than the specified timestamp.
        /// </summary>
        Task DeleteAsync(DateTime beforeUtc, CancellationToken cancellationToken = default);
    }
}
