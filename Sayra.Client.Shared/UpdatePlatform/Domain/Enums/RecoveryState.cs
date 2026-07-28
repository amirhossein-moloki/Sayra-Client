namespace Sayra.Client.Shared.UpdatePlatform.Domain.Enums
{
    /// <summary>
    /// Represents the deterministic states of the automatic Rollback & Recovery system lifecycle.
    /// </summary>
    public enum RecoveryState
    {
        Idle,
        BackupCreated,
        Monitoring,
        RecoveryRequired,
        RollingBack,
        Restoring,
        Verifying,
        Completed,
        Failed
    }
}
