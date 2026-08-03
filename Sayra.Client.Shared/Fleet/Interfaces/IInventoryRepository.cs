using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Repository managing workstation hardware and software inventories.
    /// </summary>
    public interface IInventoryRepository
    {
        /// <summary>
        /// Saves or updates the hardware and software asset inventory details of a workstation.
        /// </summary>
        Task<bool> SaveAsync(string machineId, MachineInventory inventory, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the hardware and software asset inventory details of a workstation.
        /// </summary>
        Task<MachineInventory?> GetAsync(string machineId, CancellationToken ct = default);
    }
}
