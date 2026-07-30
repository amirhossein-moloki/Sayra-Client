using System;

namespace Sayra.Client.Shared.Models.Recovery.Events
{
    /// <summary>
    /// Raised when the resilience configuration has been loaded successfully for the first time.
    /// </summary>
    public record ConfigurationLoadedEvent(
        ResilienceConfiguration Configuration,
        string CorrelationId,
        DateTime Timestamp);

    /// <summary>
    /// Raised when the resilience configuration has been reloaded and applied atomically at runtime.
    /// </summary>
    public record ConfigurationReloadedEvent(
        ResilienceConfiguration NewConfiguration,
        string CorrelationId,
        DateTime Timestamp);

    /// <summary>
    /// Raised when a resilience configuration validation check fails.
    /// </summary>
    public record ConfigurationValidationFailedEvent(
        string ErrorDetails,
        string CorrelationId,
        DateTime Timestamp);

    /// <summary>
    /// Raised when a specific subsystem recovery policy has been dynamically updated.
    /// </summary>
    public record PolicyUpdatedEvent(
        string SubsystemName,
        Sayra.Client.Shared.Models.Recovery.Policies.RecoveryPolicy NewPolicy,
        string CorrelationId,
        DateTime Timestamp);
}
