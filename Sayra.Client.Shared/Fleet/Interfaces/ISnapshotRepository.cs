using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Repository managing persistent MachineSnapshot records.
    /// </summary>
    public interface ISnapshotRepository
    {
        /// <summary>
        /// Saves or updates a workstation state snapshot.
        /// </summary>
        Task<bool> SaveAsync(MachineSnapshot snapshot, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a workstation's current state snapshot.
        /// </summary>
        Task<MachineSnapshot?> GetAsync(string machineId, CancellationToken ct = default);
    }
}
