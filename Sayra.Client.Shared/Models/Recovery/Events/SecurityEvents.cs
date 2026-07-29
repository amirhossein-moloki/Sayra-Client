using System;

namespace Sayra.Client.Shared.Models.Recovery.Events
{
    /// <summary>
    /// Raised when a security validation or integrity check begins.
    /// </summary>
    public record SecurityValidationStartedEvent(
        string CorrelationId,
        string ValidationType,
        string Target,
        DateTime Timestamp);

    /// <summary>
    /// Raised when a security validation completed successfully without direct tampering detection.
    /// </summary>
    public record SecurityValidationCompletedEvent(
        string CorrelationId,
        string ValidationType,
        string Target,
        TimeSpan Duration,
        SecurityValidationState State,
        DateTime Timestamp);

    /// <summary>
    /// Raised when a security validation fails due to missing components or minor config issues.
    /// </summary>
    public record SecurityValidationFailedEvent(
        string CorrelationId,
        string ValidationType,
        string Target,
        TimeSpan Duration,
        string Error,
        DateTime Timestamp);

    /// <summary>
    /// Raised when a direct integrity violation (checksum mismatch) is detected.
    /// </summary>
    public record IntegrityViolationDetectedEvent(
        string CorrelationId,
        string ValidationType,
        string Target,
        string ExpectedHash,
        string ComputedHash,
        DateTime Timestamp);

    /// <summary>
    /// Raised when a digital signature or certification validation fails.
    /// </summary>
    public record SignatureValidationFailedEvent(
        string CorrelationId,
        string ValidationType,
        string Target,
        string Error,
        DateTime Timestamp);

    /// <summary>
    /// Raised when direct tampering or active manipulation is identified.
    /// </summary>
    public record TamperDetectedEvent(
        string CorrelationId,
        string ValidationType,
        string Target,
        string Detail,
        DateTime Timestamp);

    /// <summary>
    /// Raised when a non-critical security/validation issue is detected.
    /// </summary>
    public record ValidationWarningEvent(
        string CorrelationId,
        string ValidationType,
        string Target,
        string Detail,
        DateTime Timestamp);
}
