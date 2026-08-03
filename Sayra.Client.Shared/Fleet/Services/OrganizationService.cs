using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Highly reliable organizational service implementation for regions and departments.
    /// </summary>
    public class OrganizationService : IOrganizationService
    {
        private readonly IRegionRepository _regionRepo;
        private readonly IDepartmentRepository _deptRepo;
        private readonly ILogger<OrganizationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrganizationService"/> class.
        /// </summary>
        public OrganizationService(
            IRegionRepository regionRepo,
            IDepartmentRepository deptRepo,
            ILogger<OrganizationService> logger)
        {
            _regionRepo = regionRepo ?? throw new ArgumentNullException(nameof(regionRepo));
            _deptRepo = deptRepo ?? throw new ArgumentNullException(nameof(deptRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<bool> SaveRegionAsync(FleetRegion region, string? parentRegionId, CancellationToken ct = default)
        {
            _logger.LogInformation("Saving region '{RegionId}' with parent '{ParentId}'", region?.RegionId, parentRegionId);
            return _regionRepo.SaveAsync(region!, parentRegionId, ct);
        }

        /// <inheritdoc />
        public Task<bool> DeleteRegionAsync(string regionId, CancellationToken ct = default)
        {
            _logger.LogInformation("Deleting region '{RegionId}'", regionId);
            return _regionRepo.DeleteAsync(regionId, ct);
        }

        /// <inheritdoc />
        public Task<FleetRegion?> GetRegionAsync(string regionId, CancellationToken ct = default)
        {
            return _regionRepo.GetAsync(regionId, ct);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<FleetRegion>> GetAllRegionsAsync(CancellationToken ct = default)
        {
            return _regionRepo.GetAllAsync(ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<FleetRegion>> GetRegionHierarchyAsync(string regionId, CancellationToken ct = default)
        {
            var hierarchy = new List<FleetRegion>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string? currentId = regionId;
            while (!string.IsNullOrEmpty(currentId))
            {
                if (visited.Contains(currentId))
                {
                    _logger.LogWarning("Cyclic reference detected in region hierarchy for '{RegionId}'", currentId);
                    break; // Prevent infinite loop
                }

                visited.Add(currentId);
                var region = await _regionRepo.GetAsync(currentId, ct);
                if (region == null) break;

                hierarchy.Add(region);
                currentId = await _regionRepo.GetParentRegionIdAsync(currentId, ct);
            }

            return hierarchy;
        }

        /// <inheritdoc />
        public Task<bool> SaveDepartmentAsync(FleetDepartment department, string? parentDepartmentId, CancellationToken ct = default)
        {
            _logger.LogInformation("Saving department '{DeptId}' with parent '{ParentId}'", department?.DepartmentId, parentDepartmentId);
            return _deptRepo.SaveAsync(department!, parentDepartmentId, ct);
        }

        /// <inheritdoc />
        public Task<bool> DeleteDepartmentAsync(string departmentId, CancellationToken ct = default)
        {
            _logger.LogInformation("Deleting department '{DeptId}'", departmentId);
            return _deptRepo.DeleteAsync(departmentId, ct);
        }

        /// <inheritdoc />
        public Task<FleetDepartment?> GetDepartmentAsync(string departmentId, CancellationToken ct = default)
        {
            return _deptRepo.GetAsync(departmentId, ct);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<FleetDepartment>> GetAllDepartmentsAsync(CancellationToken ct = default)
        {
            return _deptRepo.GetAllAsync(ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<FleetDepartment>> GetDepartmentHierarchyAsync(string departmentId, CancellationToken ct = default)
        {
            var hierarchy = new List<FleetDepartment>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string? currentId = departmentId;
            while (!string.IsNullOrEmpty(currentId))
            {
                if (visited.Contains(currentId))
                {
                    _logger.LogWarning("Cyclic reference detected in department hierarchy for '{DeptId}'", currentId);
                    break; // Prevent infinite loop
                }

                visited.Add(currentId);
                var dept = await _deptRepo.GetAsync(currentId, ct);
                if (dept == null) break;

                hierarchy.Add(dept);
                currentId = await _deptRepo.GetParentDepartmentIdAsync(currentId, ct);
            }

            return hierarchy;
        }

        /// <inheritdoc />
        public async Task<bool> ValidateHierarchyCyclesAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Validating hierarchies for cycle detection...");

            // 1. Validate Regions
            var regions = await _regionRepo.GetAllAsync(ct);
            foreach (var region in regions)
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string? parentId = region.RegionId;

                while (!string.IsNullOrEmpty(parentId))
                {
                    if (visited.Contains(parentId))
                    {
                        _logger.LogError("Cycle detected in Region hierarchy at Region ID '{Id}'", parentId);
                        return false; // Loop detected!
                    }
                    visited.Add(parentId);
                    parentId = await _regionRepo.GetParentRegionIdAsync(parentId, ct);
                }
            }

            // 2. Validate Departments
            var depts = await _deptRepo.GetAllAsync(ct);
            foreach (var dept in depts)
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string? parentId = dept.DepartmentId;

                while (!string.IsNullOrEmpty(parentId))
                {
                    if (visited.Contains(parentId))
                    {
                        _logger.LogError("Cycle detected in Department hierarchy at Department ID '{Id}'", parentId);
                        return false; // Loop detected!
                    }
                    visited.Add(parentId);
                    parentId = await _deptRepo.GetParentDepartmentIdAsync(parentId, ct);
                }
            }

            _logger.LogInformation("Hierarchy cycle validation completed successfully. No cycles found.");
            return true;
        }
    }
}
