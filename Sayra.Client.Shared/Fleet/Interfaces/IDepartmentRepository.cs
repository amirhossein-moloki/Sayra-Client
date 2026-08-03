using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Repository managing organizational departments.
    /// </summary>
    public interface IDepartmentRepository
    {
        /// <summary>
        /// Saves or updates a department.
        /// </summary>
        Task<bool> SaveAsync(FleetDepartment department, string? parentDepartmentId, CancellationToken ct = default);

        /// <summary>
        /// Deletes an organizational department.
        /// </summary>
        Task<bool> DeleteAsync(string departmentId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a department by identifier.
        /// </summary>
        Task<FleetDepartment?> GetAsync(string departmentId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the parent department ID for a given department.
        /// </summary>
        Task<string?> GetParentDepartmentIdAsync(string departmentId, CancellationToken ct = default);

        /// <summary>
        /// Gets all registered departments.
        /// </summary>
        Task<IReadOnlyList<FleetDepartment>> GetAllAsync(CancellationToken ct = default);
    }
}
