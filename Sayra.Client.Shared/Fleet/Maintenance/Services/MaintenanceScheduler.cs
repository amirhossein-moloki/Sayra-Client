using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Fleet.Maintenance.Interfaces;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;

namespace Sayra.Client.Shared.Models.Phase9.Events
{
    /// <summary>
    /// Event triggered when a new maintenance schedule is registered.
    /// </summary>
    public record MaintenanceScheduled(string ScheduleId, string WindowId, MaintenanceWindowType Category) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a maintenance execution task fails.
    /// </summary>
    public record MaintenanceFailed(string ScheduleId, string WindowId, string ErrorMessage) : Phase9BaseEvent;
}

namespace Sayra.Client.Shared.Fleet.Maintenance.Services
{
    /// <summary>
    /// Implements <see cref="IMaintenanceScheduler"/> and manages scheduled maintenance calendar state.
    /// </summary>
    public class MaintenanceScheduler : IMaintenanceScheduler
    {
        private readonly ILogger<MaintenanceScheduler> _logger;
        private readonly IMaintenanceRepository _maintenanceRepository;
        private readonly IEventDispatcher _eventDispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="MaintenanceScheduler"/> class.
        /// </summary>
        public MaintenanceScheduler(
            ILogger<MaintenanceScheduler> logger,
            IMaintenanceRepository maintenanceRepository,
            IEventDispatcher eventDispatcher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maintenanceRepository = maintenanceRepository ?? throw new ArgumentNullException(nameof(maintenanceRepository));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        }

        /// <inheritdoc />
        public async Task<bool> ScheduleMaintenanceAsync(MaintenanceSchedule schedule, CancellationToken ct = default)
        {
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));

            _logger.LogInformation("Scheduling future maintenance task: ScheduleId='{ScheduleId}'", schedule.ScheduleId);

            // Save to secure SQLCipher repository
            bool success = await _maintenanceRepository.SaveScheduleAsync(schedule, ct);
            if (success)
            {
                // Dispatch Scheduled Event
                _eventDispatcher.Dispatch(new MaintenanceScheduled(
                    schedule.ScheduleId,
                    schedule.Window?.WindowId ?? string.Empty,
                    schedule.Window?.Category ?? MaintenanceWindowType.SystemCleanup
                ));
            }

            return success;
        }

        /// <inheritdoc />
        public async Task<bool> CancelScheduledMaintenanceAsync(string scheduleId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(scheduleId)) return false;

            _logger.LogInformation("Cancelling scheduled maintenance task: ScheduleId='{ScheduleId}'", scheduleId);

            var schedule = await _maintenanceRepository.GetScheduleAsync(scheduleId, ct);
            if (schedule == null)
            {
                _logger.LogWarning("Maintenance schedule with ID '{ScheduleId}' not found for cancellation.", scheduleId);
                return false;
            }

            // Verify State Machine Transition (Part 13)
            if (schedule.State == MaintenanceStatus.Completed || schedule.State == MaintenanceStatus.Failed)
            {
                _logger.LogWarning("Cannot cancel maintenance schedule '{ScheduleId}' because it is in a terminal state: {State}.", scheduleId, schedule.State);
                return false;
            }

            var cancelledSchedule = schedule with { State = MaintenanceStatus.Cancelled };
            bool success = await _maintenanceRepository.SaveScheduleAsync(cancelledSchedule, ct);
            if (success)
            {
                // Dispatch Cancelled Event
                _eventDispatcher.Dispatch(new MaintenanceCancelled(
                    scheduleId,
                    schedule.Window?.WindowId ?? string.Empty
                ));
            }

            return success;
        }
    }
}
