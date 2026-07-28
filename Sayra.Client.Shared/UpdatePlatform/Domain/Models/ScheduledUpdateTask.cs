using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents a scheduled update task orchestrated by the Update Scheduler.
    /// </summary>
    public class ScheduledUpdateTask
    {
        /// <summary>
        /// Unique task identifier.
        /// </summary>
        public Guid TaskId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Name of the task (e.g. "UpdateCheck", "Download", "Install").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if the task repeats periodically.
        /// </summary>
        public bool IsRecurring { get; set; }

        /// <summary>
        /// Interval between executions if recurring.
        /// </summary>
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// Timestamp when the task was last executed.
        /// </summary>
        public DateTime? LastRunTime { get; set; }

        /// <summary>
        /// Next planned execution time.
        /// </summary>
        public DateTime NextRunTime { get; set; }

        /// <summary>
        /// Indicates if this task is currently actively executing.
        /// </summary>
        public bool IsRunning { get; set; }
    }
}
