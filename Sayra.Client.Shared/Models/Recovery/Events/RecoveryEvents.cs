using System;

namespace Sayra.Client.Shared.Models.Recovery.Events
{
    public record RecoveryStartedEvent(
        string SubsystemName,
        string ActionTaken,
        int AttemptNumber,
        string CorrelationId,
        DateTime Timestamp);

    public record RecoveryCompletedEvent(
        string SubsystemName,
        string ActionTaken,
        int AttemptNumber,
        string CorrelationId,
        TimeSpan Duration,
        DateTime Timestamp);

    public record RecoveryFailedEvent(
        string SubsystemName,
        string ActionTaken,
        int AttemptNumber,
        string CorrelationId,
        TimeSpan Duration,
        string Error,
        DateTime Timestamp);

    public record RecoveryCancelledEvent(
        string SubsystemName,
        string CorrelationId,
        DateTime Timestamp);

    public record RecoveryEscalatedEvent(
        string SubsystemName,
        string Reason,
        string CorrelationId,
        DateTime Timestamp);

    public record RecoveryLoopDetectedEvent(
        string SubsystemName,
        int FailureCount,
        TimeSpan Window,
        string CorrelationId,
        DateTime Timestamp);

    public record RecoveryDependencyBlockedEvent(
        string SubsystemName,
        string BlockedBySubsystem,
        string CorrelationId,
        DateTime Timestamp);
}
