using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sayra.Client.Shared.Models.Phase9.Domain
{
    /// <summary>
    /// Represents a specific task to be scheduled and performed during maintenance.
    /// </summary>
    public record MaintenanceTask
    {
        /// <summary>
        /// Gets the unique identifier for the maintenance task.
        /// </summary>
        [JsonPropertyName("taskId")]
        public string TaskId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the display name of the task.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the category/type of the maintenance operation.
        /// </summary>
        [JsonPropertyName("category")]
        public string Category { get; init; } = string.Empty;

        /// <summary>
        /// Gets whether the task requires administrative elevation.
        /// </summary>
        [JsonPropertyName("requiresElevation")]
        public bool RequiresElevation { get; init; }

        /// <summary>
        /// Gets execution timeout parameters.
        /// </summary>
        [JsonPropertyName("timeout")]
        public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets custom parameters passed to the task executor.
        /// </summary>
        [JsonPropertyName("parameters")]
        public Dictionary<string, string> Parameters { get; init; } = new();

        /// <summary>
        /// Validates the structure and properties of the maintenance task.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(TaskId) &&
                   !string.IsNullOrWhiteSpace(Name) &&
                   !string.IsNullOrWhiteSpace(Category);
        }
    }

    /// <summary>
    /// Governs policies, preconditions, and execution constraints of a maintenance schedule.
    /// </summary>
    public record MaintenancePolicy
    {
        /// <summary>
        /// Gets the unique identifier for the maintenance policy.
        /// </summary>
        [JsonPropertyName("policyId")]
        public string PolicyId { get; init; } = string.Empty;

        /// <summary>
        /// Gets whether active user sessions can be forcefully terminated.
        /// </summary>
        [JsonPropertyName("allowForceSessionTermination")]
        public bool AllowForceSessionTermination { get; init; }

        /// <summary>
        /// Gets whether the system should automatically reboot post-maintenance.
        /// </summary>
        [JsonPropertyName("requirePostReboot")]
        public bool RequirePostReboot { get; init; }

        /// <summary>
        /// Gets the retry policy settings (e.g., maximum retries).
        /// </summary>
        [JsonPropertyName("maxRetryAttempts")]
        public int MaxRetryAttempts { get; init; }

        /// <summary>
        /// Gets the blackout or exclusion dates where maintenance must never run.
        /// </summary>
        [JsonPropertyName("blackoutDates")]
        public List<DateTime> BlackoutDates { get; init; } = new();

        /// <summary>
        /// Validates the structure and properties of the maintenance policy.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(PolicyId);
        }
    }

    /// <summary>
    /// Tracks the active execution details of a scheduled maintenance schedule on a workstation.
    /// </summary>
    public record MaintenanceExecution
    {
        /// <summary>
        /// Gets the unique identifier for this specific maintenance execution run.
        /// </summary>
        [JsonPropertyName("executionId")]
        public string ExecutionId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the associated schedule identifier.
        /// </summary>
        [JsonPropertyName("scheduleId")]
        public string ScheduleId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the targeted machine identifier.
        /// </summary>
        [JsonPropertyName("machineId")]
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the execution status.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; init; } = "Scheduled";

        /// <summary>
        /// Gets the UTC timestamp when the execution started.
        /// </summary>
        [JsonPropertyName("startTimeUtc")]
        public DateTime? StartTimeUtc { get; init; }

        /// <summary>
        /// Gets the UTC timestamp when the execution finished.
        /// </summary>
        [JsonPropertyName("endTimeUtc")]
        public DateTime? EndTimeUtc { get; init; }

        /// <summary>
        /// Gets the gathered text output or console logs.
        /// </summary>
        [JsonPropertyName("outputLogs")]
        public string OutputLogs { get; init; } = string.Empty;

        /// <summary>
        /// Gets the failure error message (if the execution failed).
        /// </summary>
        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; init; } = string.Empty;

        /// <summary>
        /// Validates the structure and properties of the maintenance execution.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(ExecutionId) &&
                   !string.IsNullOrWhiteSpace(ScheduleId) &&
                   !string.IsNullOrWhiteSpace(MachineId);
        }
    }

    /// <summary>
    /// Represents historical completion records of a maintenance window.
    /// </summary>
    public record MaintenanceHistory
    {
        /// <summary>
        /// Gets the unique history identifier.
        /// </summary>
        [JsonPropertyName("historyId")]
        public string HistoryId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the associated schedule identifier.
        /// </summary>
        [JsonPropertyName("scheduleId")]
        public string ScheduleId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the overall execution outcome status.
        /// </summary>
        [JsonPropertyName("outcomeStatus")]
        public string OutcomeStatus { get; init; } = string.Empty;

        /// <summary>
        /// Gets the combined list of affected workstations.
        /// </summary>
        [JsonPropertyName("affectedMachines")]
        public List<string> AffectedMachines { get; init; } = new();

        /// <summary>
        /// Gets the UTC start timestamp of the maintenance window.
        /// </summary>
        [JsonPropertyName("startTimeUtc")]
        public DateTime StartTimeUtc { get; init; }

        /// <summary>
        /// Gets the UTC completion timestamp of the maintenance window.
        /// </summary>
        [JsonPropertyName("endTimeUtc")]
        public DateTime EndTimeUtc { get; init; }

        /// <summary>
        /// Gets the generated execution report/summary description.
        /// </summary>
        [JsonPropertyName("summary")]
        public string Summary { get; init; } = string.Empty;

        /// <summary>
        /// Validates the structure and properties of the maintenance history record.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(HistoryId) &&
                   !string.IsNullOrWhiteSpace(ScheduleId);
        }
    }

    /// <summary>
    /// Represents a warning or scheduling notice delivered regarding planned maintenance.
    /// </summary>
    public record MaintenanceNotification
    {
        /// <summary>
        /// Gets the unique notification tracker identifier.
        /// </summary>
        [JsonPropertyName("notificationId")]
        public string NotificationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the associated schedule identifier.
        /// </summary>
        [JsonPropertyName("scheduleId")]
        public string ScheduleId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the message content body.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Gets the timestamp when the alert should be broadcast.
        /// </summary>
        [JsonPropertyName("broadcastAtUtc")]
        public DateTime BroadcastAtUtc { get; init; }

        /// <summary>
        /// Gets the list of target workstation IDs to deliver the warning to.
        /// </summary>
        [JsonPropertyName("recipientMachineIds")]
        public List<string> RecipientMachineIds { get; init; } = new();

        /// <summary>
        /// Gets whether the alert has been acknowledged or successfully dispatched.
        /// </summary>
        [JsonPropertyName("isSent")]
        public bool IsSent { get; init; }

        /// <summary>
        /// Validates the structure and properties of the maintenance notification.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(NotificationId) &&
                   !string.IsNullOrWhiteSpace(ScheduleId) &&
                   !string.IsNullOrWhiteSpace(Message);
        }
    }
}
