using System;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Models.Phase9.Events
{
    /// <summary>
    /// Event triggered when a bulk operation is created/registered in the system.
    /// </summary>
    public record BulkOperationCreated(string BulkOperationId, string Action, int TargetCount, string OperatorId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when the execution progress of an active bulk operation changes.
    /// </summary>
    public record BulkOperationProgressChanged(string BulkOperationId, int CompletedCount, int SucceededCount, int FailedCount, double PercentageComplete) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a bulk operation rollback process is initiated.
    /// </summary>
    public record BulkOperationRollbackStarted(string BulkOperationId, string RollbackAction, int TargetCount) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a bulk operation rollback process completes.
    /// </summary>
    public record BulkOperationRollbackCompleted(string BulkOperationId, string RollbackAction, bool IsValidated, int SucceededCount, int FailedCount) : Phase9BaseEvent;
}
