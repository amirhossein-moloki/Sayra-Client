using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Maintenance.Interfaces
{
    /// <summary>
    /// Thread-safe enterprise SQLCipher storage repository for maintenance scheduling, windows, and execution runs.
    /// </summary>
    public interface IMaintenanceRepository
    {
        /// <summary>
        /// Saves or updates a maintenance schedule.
        /// </summary>
        Task<bool> SaveScheduleAsync(MaintenanceSchedule schedule, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a specific maintenance schedule by ID.
        /// </summary>
        Task<MaintenanceSchedule?> GetScheduleAsync(string scheduleId, CancellationToken ct = default);

        /// <summary>
        /// Deletes a specific maintenance schedule by ID.
        /// </summary>
        Task<bool> DeleteScheduleAsync(string scheduleId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all maintenance schedules in the system.
        /// </summary>
        Task<IReadOnlyList<MaintenanceSchedule>> GetAllSchedulesAsync(CancellationToken ct = default);

        /// <summary>
        /// Saves or updates a maintenance task execution run.
        /// </summary>
        Task<bool> SaveExecutionAsync(MaintenanceExecution execution, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a specific maintenance task execution by ID.
        /// </summary>
        Task<MaintenanceExecution?> GetExecutionAsync(string executionId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all task executions associated with a specific schedule ID.
        /// </summary>
        Task<IReadOnlyList<MaintenanceExecution>> GetExecutionsByScheduleAsync(string scheduleId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all task executions associated with a specific machine ID.
        /// </summary>
        Task<IReadOnlyList<MaintenanceExecution>> GetExecutionsByMachineAsync(string machineId, CancellationToken ct = default);

        /// <summary>
        /// Records a maintenance window history log on completion/failure.
        /// </summary>
        Task<bool> RecordHistoryAsync(MaintenanceHistory history, CancellationToken ct = default);

        /// <summary>
        /// Retrieves maintenance completion history logs.
        /// </summary>
        Task<IReadOnlyList<MaintenanceHistory>> GetHistoryAsync(CancellationToken ct = default);
    }
}
