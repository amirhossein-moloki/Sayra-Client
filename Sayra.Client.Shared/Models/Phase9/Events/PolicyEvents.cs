using System;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Models.Phase9.Events
{
    /// <summary>
    /// Event triggered when a new security or system policy template is created.
    /// </summary>
    public record PolicyCreated(string PolicyId, string Name, string Category, string CreatorOperatorId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when an existing policy definition metadata or rule structure is updated.
    /// </summary>
    public record PolicyUpdated(string PolicyId, string Name, string VersionTag, string EditorOperatorId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a specific version of a policy definition is approved and published.
    /// </summary>
    public record PolicyPublished(string PolicyId, string VersionTag, string PublisherOperatorId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a rollback sequence to a previous policy version begins.
    /// </summary>
    public record PolicyRollbackStarted(string PolicyId, string TargetVersionTag, string SourceVersionTag, string OperatorId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a rollback sequence to a historical policy version completes successfully.
    /// </summary>
    public record PolicyRollbackCompleted(string PolicyId, string RestoredVersionTag, string OperatorId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a workstation's overall compliance assessment score or compliance tier changes.
    /// </summary>
    public record ComplianceChanged(string MachineId, ComplianceStatus OldStatus, ComplianceStatus NewStatus, double NewScore) : Phase9BaseEvent;
}
