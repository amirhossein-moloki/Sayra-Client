using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Repository managing persistent MachineHealth and historical HealthSnapshot logs.
    /// </summary>
    public interface IHealthRepository
    {
        /// <summary>
        /// Saves or updates a workstation's current health scores.
        /// </summary>
        Task<bool> SaveHealthAsync(MachineHealth health, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a workstation's current health scores.
        /// </summary>
        Task<MachineHealth?> GetHealthAsync(string machineId, CancellationToken ct = default);

        /// <summary>
        /// Appends a new health snapshot to the workstation's historical log.
        /// </summary>
        Task<bool> LogSnapshotAsync(string machineId, HealthSnapshot snapshot, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a workstation's historical health logs, ordered by newest.
        /// </summary>
        Task<IReadOnlyList<HealthSnapshot>> GetHistoryAsync(string machineId, int limit = 100, CancellationToken ct = default);
    }
}
