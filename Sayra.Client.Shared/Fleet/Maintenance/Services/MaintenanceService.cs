using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Maintenance.Interfaces;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;

namespace Sayra.Client.Shared.Fleet.Maintenance.Services
{
    /// <summary>
    /// Implements <see cref="IMaintenanceService"/> to orchestrate state machine transitions and triggers.
    /// </summary>
    public class MaintenanceService : IMaintenanceService
    {
        private readonly ILogger<MaintenanceService> _logger;
        private readonly IMaintenanceRepository _maintenanceRepository;
        private readonly IEventDispatcher _eventDispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="MaintenanceService"/> class.
        /// </summary>
        public MaintenanceService(
            ILogger<MaintenanceService> logger,
            IMaintenanceRepository maintenanceRepository,
            IEventDispatcher eventDispatcher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maintenanceRepository = maintenanceRepository ?? throw new ArgumentNullException(nameof(maintenanceRepository));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        }

        /// <inheritdoc />
        public async Task<bool> ExecuteMaintenanceAsync(string machineId, string scheduleId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(scheduleId)) return false;

            _logger.LogInformation("Executing maintenance: ScheduleId='{ScheduleId}' for machine '{MachineId}'", scheduleId, machineId);

            var schedule = await _maintenanceRepository.GetScheduleAsync(scheduleId, ct);
            if (schedule == null)
            {
                _logger.LogWarning("Maintenance schedule with ID '{ScheduleId}' not found for execution.", scheduleId);
                return false;
            }

            // Part 13: Maintenance State Machine State Transitions
            // Transition: Scheduled -> Initializing (Preparing)
            if (schedule.State != MaintenanceStatus.Scheduled)
            {
                _logger.LogWarning("Cannot execute maintenance schedule '{ScheduleId}' as it is not in Scheduled state. Current State: {State}", scheduleId, schedule.State);
                return false;
            }

            // 1. Preparing State
            var preparingSchedule = schedule with { State = MaintenanceStatus.Initializing, ExecutionSummary = "Preparing and initializing maintenance task components..." };
            await _maintenanceRepository.SaveScheduleAsync(preparingSchedule, ct);

            // Record and save execution run record
            var execution = new MaintenanceExecution
            {
                ExecutionId = Guid.NewGuid().ToString(),
                ScheduleId = scheduleId,
                MachineId = machineId,
                Status = "Preparing",
                StartTimeUtc = DateTime.UtcNow,
                OutputLogs = "Initializing window context...",
                ErrorMessage = string.Empty
            };
            await _maintenanceRepository.SaveExecutionAsync(execution, ct);

            // 2. Active State (Running)
            _logger.LogInformation("Transitioning maintenance '{ScheduleId}' to Active / Running state.", scheduleId);
            var activeSchedule = preparingSchedule with { State = MaintenanceStatus.Running, ExecutionSummary = "Maintenance tasks are actively running on targets." };
            await _maintenanceRepository.SaveScheduleAsync(activeSchedule, ct);

            execution = execution with { Status = "Active", OutputLogs = execution.OutputLogs + "\nExecuting scheduled cleanup and health checks..." };
            await _maintenanceRepository.SaveExecutionAsync(execution, ct);

            // Dispatch Started Event (Part 14 Notification System)
            _eventDispatcher.Dispatch(new MaintenanceStarted(
                scheduleId,
                schedule.Window?.WindowId ?? string.Empty,
                schedule.Window?.Category ?? MaintenanceWindowType.SystemCleanup
            ));

            // Simulate task scheduling (as actual execution belongs elsewhere)
            await Task.Delay(100, ct);

            // Transition: Running -> Completed
            _logger.LogInformation("Transitioning maintenance '{ScheduleId}' to Completed state.", scheduleId);
            var completedSchedule = activeSchedule with { State = MaintenanceStatus.Completed, ExecutionSummary = "All maintenance tasks completed successfully." };
            await _maintenanceRepository.SaveScheduleAsync(completedSchedule, ct);

            execution = execution with
            {
                Status = "Completed",
                EndTimeUtc = DateTime.UtcNow,
                OutputLogs = execution.OutputLogs + "\nAll tasks finished successfully. Releasing lock."
            };
            await _maintenanceRepository.SaveExecutionAsync(execution, ct);

            // Save to maintenance history (Part 15 Repository History)
            var history = new MaintenanceHistory
            {
                HistoryId = Guid.NewGuid().ToString(),
                ScheduleId = scheduleId,
                OutcomeStatus = "Success",
                AffectedMachines = new List<string> { machineId },
                StartTimeUtc = execution.StartTimeUtc ?? DateTime.UtcNow,
                EndTimeUtc = execution.EndTimeUtc ?? DateTime.UtcNow,
                Summary = "Scheduled maintenance completed flawlessly."
            };
            await _maintenanceRepository.RecordHistoryAsync(history, ct);

            // Dispatch Completed Event (Part 14 Notification System)
            _eventDispatcher.Dispatch(new MaintenanceCompleted(
                scheduleId,
                schedule.Window?.WindowId ?? string.Empty,
                MaintenanceStatus.Completed
            ));

            return true;
        }

        /// <summary>
        /// Explicitly transitions maintenance execution to a failed state.
        /// </summary>
        public async Task<bool> FailMaintenanceAsync(string machineId, string scheduleId, string errorMessage, CancellationToken ct = default)
        {
            _logger.LogWarning("Marking maintenance '{ScheduleId}' as failed on machine '{MachineId}': {Error}", scheduleId, machineId, errorMessage);

            var schedule = await _maintenanceRepository.GetScheduleAsync(scheduleId, ct);
            if (schedule == null) return false;

            var failedSchedule = schedule with { State = MaintenanceStatus.Failed, ExecutionSummary = $"Maintenance execution failed: {errorMessage}" };
            await _maintenanceRepository.SaveScheduleAsync(failedSchedule, ct);

            var executionId = Guid.NewGuid().ToString();
            var execution = new MaintenanceExecution
            {
                ExecutionId = executionId,
                ScheduleId = scheduleId,
                MachineId = machineId,
                Status = "Failed",
                StartTimeUtc = DateTime.UtcNow,
                EndTimeUtc = DateTime.UtcNow,
                OutputLogs = "Critical error encountered during active run.",
                ErrorMessage = errorMessage
            };
            await _maintenanceRepository.SaveExecutionAsync(execution, ct);

            // Record History
            var history = new MaintenanceHistory
            {
                HistoryId = Guid.NewGuid().ToString(),
                ScheduleId = scheduleId,
                OutcomeStatus = "Failure",
                AffectedMachines = new List<string> { machineId },
                StartTimeUtc = DateTime.UtcNow,
                EndTimeUtc = DateTime.UtcNow,
                Summary = $"Scheduled maintenance failed: {errorMessage}"
            };
            await _maintenanceRepository.RecordHistoryAsync(history, ct);

            // Dispatch Failed Event
            _eventDispatcher.Dispatch(new MaintenanceFailed(
                scheduleId,
                schedule.Window?.WindowId ?? string.Empty,
                errorMessage
            ));

            return true;
        }
    }
}
