using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Contract for the Enterprise Update Scheduler.
    /// </summary>
    public interface IUpdateScheduler
    {
        /// <summary>
        /// Starts the background scheduling system.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the background scheduling system.
        /// </summary>
        void Stop();

        /// <summary>
        /// Forces an immediate update check, ignoring intervals.
        /// </summary>
        Task TriggerImmediateCheckAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Schedules a specific update task manually.
        /// </summary>
        void ScheduleTask(ScheduledUpdateTask task);

        /// <summary>
        /// Retrieves the current execution state of scheduled tasks.
        /// </summary>
        ScheduledUpdateTask[] GetScheduledTasks();
    }
}
