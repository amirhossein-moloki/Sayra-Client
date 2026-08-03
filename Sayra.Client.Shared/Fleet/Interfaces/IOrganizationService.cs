using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Contract for managing organizational departments, centers, regions, and hierarchies.
    /// </summary>
    public interface IOrganizationService
    {
        /// <summary>
        /// Saves or updates a regional division.
        /// </summary>
        Task<bool> SaveRegionAsync(FleetRegion region, string? parentRegionId, CancellationToken ct = default);

        /// <summary>
        /// Removes a regional division.
        /// </summary>
        Task<bool> DeleteRegionAsync(string regionId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a regional division.
        /// </summary>
        Task<FleetRegion?> GetRegionAsync(string regionId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all registered regions.
        /// </summary>
        Task<IReadOnlyList<FleetRegion>> GetAllRegionsAsync(CancellationToken ct = default);

        /// <summary>
        /// Traverses and returns parent regions from child up to the root regional area.
        /// </summary>
        Task<IReadOnlyList<FleetRegion>> GetRegionHierarchyAsync(string regionId, CancellationToken ct = default);

        /// <summary>
        /// Saves or updates an organizational department.
        /// </summary>
        Task<bool> SaveDepartmentAsync(FleetDepartment department, string? parentDepartmentId, CancellationToken ct = default);

        /// <summary>
        /// Removes an organizational department.
        /// </summary>
        Task<bool> DeleteDepartmentAsync(string departmentId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves an organizational department.
        /// </summary>
        Task<FleetDepartment?> GetDepartmentAsync(string departmentId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all registered organizational departments.
        /// </summary>
        Task<IReadOnlyList<FleetDepartment>> GetAllDepartmentsAsync(CancellationToken ct = default);

        /// <summary>
        /// Traverses and returns parent departments from child up to the root department division.
        /// </summary>
        Task<IReadOnlyList<FleetDepartment>> GetDepartmentHierarchyAsync(string departmentId, CancellationToken ct = default);

        /// <summary>
        /// Performs thorough cycle detection verification on region and department hierarchies.
        /// </summary>
        Task<bool> ValidateHierarchyCyclesAsync(CancellationToken ct = default);
    }
}
