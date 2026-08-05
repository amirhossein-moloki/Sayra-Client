using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Models.Phase9.Domain
{
    /// <summary>
    /// Type of workstation target filtering for bulk operations.
    /// </summary>
    public enum BulkTargetType
    {
        /// <summary>
        /// A list of individual machine IDs.
        /// </summary>
        Individual,

        /// <summary>
        /// A static group of machines.
        /// </summary>
        StaticGroup,

        /// <summary>
        /// A dynamically evaluated group of machines.
        /// </summary>
        DynamicGroup,

        /// <summary>
        /// All machines belonging to a specific region.
        /// </summary>
        Region,

        /// <summary>
        /// All machines belonging to a specific gaming center.
        /// </summary>
        GamingCenter,

        /// <summary>
        /// Machines matching a specific metadata tag.
        /// </summary>
        Tag,

        /// <summary>
        /// Machines matching a specific health group classification.
        /// </summary>
        HealthGroup
    }

    /// <summary>
    /// Classification of bulk operation failures.
    /// </summary>
    public enum BulkFailureType
    {
        /// <summary>
        /// General network issues.
        /// </summary>
        NetworkFailure,

        /// <summary>
        /// Timeout expired during command execution.
        /// </summary>
        Timeout,

        /// <summary>
        /// Permission or security validation failure.
        /// </summary>
        PermissionFailure,

        /// <summary>
        /// Machine was offline when execution was scheduled.
        /// </summary>
        MachineOffline,

        /// <summary>
        /// Fallback classification for other unhandled issues.
        /// </summary>
        UnknownFailure
    }

    /// <summary>
    /// Immutable record representing a targeting filter criteria for bulk execution.
    /// </summary>
    public record BulkOperationTarget
    {
        /// <summary>
        /// Gets the type of targeting mechanism.
        /// </summary>
        public BulkTargetType TargetType { get; init; }

        /// <summary>
        /// Gets the target value (e.g. group name, region code, tag key-value string, or machine ID).
        /// </summary>
        public string TargetValue { get; init; } = string.Empty;

        /// <summary>
        /// Validates that the target properties are structurally sound.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(TargetValue);
        }
    }

    /// <summary>
    /// Immutable record representing a failure encountered on an individual workstation.
    /// </summary>
    public record BulkOperationFailure
    {
        /// <summary>
        /// Gets the unique identifier of the target machine.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the categorized failure type.
        /// </summary>
        public BulkFailureType FailureType { get; init; }

        /// <summary>
        /// Gets the error details or exception message.
        /// </summary>
        public string ErrorMessage { get; init; } = string.Empty;

        /// <summary>
        /// Gets the UTC timestamp when the failure occurred.
        /// </summary>
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Validates that the failure contains all required information.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(MachineId) && !string.IsNullOrWhiteSpace(ErrorMessage);
        }
    }

    /// <summary>
    /// Immutable record representing execution policies and configuration for bulk operations.
    /// </summary>
    public record BulkOperationPolicy
    {
        /// <summary>
        /// Gets the maximum allowed concurrent execution operations.
        /// </summary>
        public int MaxConcurrency { get; init; } = 10;

        /// <summary>
        /// Gets the execution timeout for each individual workstation operation.
        /// </summary>
        public TimeSpan IndividualTimeout { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets the maximum retry attempts for transient errors.
        /// </summary>
        public int MaxRetries { get; init; } = 3;

        /// <summary>
        /// Gets the base delay for retry backoff calculations.
        /// </summary>
        public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets whether the entire bulk operation should rollback on failure.
        /// </summary>
        public bool RollbackOnFailure { get; init; }

        /// <summary>
        /// Validates policy configuration ranges.
        /// </summary>
        public bool Validate()
        {
            return MaxConcurrency > 0 && IndividualTimeout > TimeSpan.Zero && MaxRetries >= 0 && RetryBaseDelay >= TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Immutable record representing the overall execution details and stats for a completed bulk operation.
    /// </summary>
    public record BulkOperationSummary
    {
        /// <summary>
        /// Gets the bulk operation unique tracker identifier.
        /// </summary>
        public string BulkOperationId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the total number of workstations targeted.
        /// </summary>
        public int TotalCount { get; init; }

        /// <summary>
        /// Gets the number of successful operations.
        /// </summary>
        public int SucceededCount { get; init; }

        /// <summary>
        /// Gets the number of failed operations.
        /// </summary>
        public int FailedCount { get; init; }

        /// <summary>
        /// Gets the number of skipped operations.
        /// </summary>
        public int SkippedCount { get; init; }

        /// <summary>
        /// Gets the combined execution duration.
        /// </summary>
        public TimeSpan CombinedDuration { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the administrator who initiated the action.
        /// </summary>
        public string OperatorId { get; init; } = string.Empty;
    }

    /// <summary>
    /// Immutable record representing the live tracking state of an individual workstation's bulk task.
    /// </summary>
    public record BulkOperationExecution
    {
        /// <summary>
        /// Gets the targeted workstation identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the active command status for this machine.
        /// </summary>
        public Sayra.Client.Shared.Models.Phase9.Enums.CommandStatus Status { get; init; } = Sayra.Client.Shared.Models.Phase9.Enums.CommandStatus.Pending;

        /// <summary>
        /// Gets the start timestamp.
        /// </summary>
        public DateTime? StartedAtUtc { get; init; }

        /// <summary>
        /// Gets the completion timestamp.
        /// </summary>
        public DateTime? CompletedAtUtc { get; init; }

        /// <summary>
        /// Gets the number of execution retry attempts taken.
        /// </summary>
        public int AttemptNumber { get; init; }
    }
}
