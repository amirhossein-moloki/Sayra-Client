using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Repository managing persistent Workstation (MachineInfo) records.
    /// </summary>
    public interface IMachineRepository
    {
        /// <summary>
        /// Saves or updates a workstation's core details.
        /// </summary>
        Task<bool> SaveAsync(MachineInfo machine, CancellationToken ct = default);

        /// <summary>
        /// Deletes a workstation from the system.
        /// </summary>
        Task<bool> DeleteAsync(string machineId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a specific workstation's details by id.
        /// </summary>
        Task<MachineInfo?> GetAsync(string machineId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all registered workstations.
        /// </summary>
        Task<IReadOnlyList<MachineInfo>> GetAllAsync(CancellationToken ct = default);
    }
}
