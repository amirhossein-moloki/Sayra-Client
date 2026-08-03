using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Aggregated high-level statistics and division counts.
    /// </summary>
    public class FleetStatistics
    {
        /// <summary>Total workstations count.</summary>
        public int TotalMachinesCount { get; set; }

        /// <summary>Count of machines online.</summary>
        public int OnlineCount { get; set; }

        /// <summary>Count of machines offline.</summary>
        public int OfflineCount { get; set; }

        /// <summary>Count of machines in active game sessions.</summary>
        public int InSessionCount { get; set; }

        /// <summary>Count of machines undergoing maintenance.</summary>
        public int MaintenanceCount { get; set; }

        /// <summary>Count of administratively locked machines.</summary>
        public int LockedCount { get; set; }

        /// <summary>Average computed health score across the entire fleet.</summary>
        public double AverageHealthScore { get; set; }

        /// <summary>Healthy machines count.</summary>
        public int HealthyCount { get; set; }

        /// <summary>Warning machines count.</summary>
        public int WarningCount { get; set; }

        /// <summary>Critical performance machines count.</summary>
        public int CriticalCount { get; set; }

        /// <summary>Emergency issue machines count.</summary>
        public int EmergencyCount { get; set; }

        /// <summary>Aggregated RAM size across the fleet in GB.</summary>
        public long TotalRamGb { get; set; }

        /// <summary>Counts of distinct operating system versions detected.</summary>
        public Dictionary<string, int> OSVersionCounts { get; set; } = new();
    }

    /// <summary>
    /// High-level query service contract for projections and aggregations.
    /// </summary>
    public interface IFleetQueryService
    {
        /// <summary>
        /// Compiles high-level statistical aggregates for the entire fleet.
        /// </summary>
        Task<FleetStatistics> GetFleetStatisticsAsync(CancellationToken ct = default);

        /// <summary>
        /// Retrieves members of a specific fleet group.
        /// </summary>
        Task<IReadOnlyList<MachineInfo>> QueryByGroupAsync(string groupId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves workstations within a specific region.
        /// </summary>
        Task<IReadOnlyList<MachineInfo>> QueryByRegionAsync(string regionId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves workstations associated with an organizational department.
        /// </summary>
        Task<IReadOnlyList<MachineInfo>> QueryByDepartmentAsync(string departmentId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves workstations filtered by their active Status.
        /// </summary>
        Task<IReadOnlyList<MachineInfo>> QueryByStatusAsync(string status, CancellationToken ct = default);

        /// <summary>
        /// Retrieves workstations filtered by their computed health state.
        /// </summary>
        Task<IReadOnlyList<MachineInfo>> QueryByHealthStatusAsync(string healthStatus, CancellationToken ct = default);
    }
}
