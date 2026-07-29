using System;

namespace Sayra.Client.Shared.Models.Recovery.Events
{
    /// <summary>
    /// Raised when report generation has started.
    /// </summary>
    public record DiagnosticsGenerationStartedEvent(
        string CorrelationId,
        ReportType ReportType,
        DateTime Timestamp);

    /// <summary>
    /// Raised when report generation completes successfully.
    /// </summary>
    public record DiagnosticsGenerationCompletedEvent(
        string CorrelationId,
        ReportType ReportType,
        TimeSpan Duration,
        DateTime Timestamp);

    /// <summary>
    /// Raised when report generation fails.
    /// </summary>
    public record DiagnosticsGenerationFailedEvent(
        string CorrelationId,
        ReportType ReportType,
        string Error,
        string? ExceptionDetails,
        DateTime Timestamp);

    /// <summary>
    /// Raised when a report is successfully saved/persisted to the local storage.
    /// </summary>
    public record ReportPersistedEvent(
        string CorrelationId,
        ReportType ReportType,
        string Format,
        string OutputPath,
        DateTime Timestamp);

    /// <summary>
    /// Raised when a report has been successfully exported.
    /// </summary>
    public record DiagnosticsExportedEvent(
        string CorrelationId,
        ReportType ReportType,
        string Format,
        string OutputPath,
        DateTime Timestamp);
}
