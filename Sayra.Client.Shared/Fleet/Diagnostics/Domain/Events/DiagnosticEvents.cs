using System;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;

namespace Sayra.Client.Shared.Models.Phase9.Events
{
    /// <summary>
    /// Event triggered when a workstation diagnostics execution starts.
    /// </summary>
    public record DiagnosticsStarted(string MachineId, string DiagnosticId, string OperatorId) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when the progress of a running diagnostics session changes.
    /// </summary>
    public record DiagnosticsProgressChanged(string MachineId, string DiagnosticId, double ProgressPercentage, string CurrentStep) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a single diagnostic report is successfully created.
    /// </summary>
    public record DiagnosticReportCreated(string MachineId, string DiagnosticId, string ReportId, DiagnosticReportType ReportType) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a compressed diagnostics package is compiled and stored.
    /// </summary>
    public record DiagnosticPackageCreated(string MachineId, string DiagnosticId, string PackageId, string ArchiveFileName, string IntegrityHash) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a diagnostics execution session completes successfully.
    /// </summary>
    public record DiagnosticsCompleted(string MachineId, string DiagnosticId, long DurationMs) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when a diagnostics execution session fails.
    /// </summary>
    public record DiagnosticsFailed(string MachineId, string DiagnosticId, string ErrorMessage) : Phase9BaseEvent;

    /// <summary>
    /// Event triggered when an anomaly or warning is discovered during diagnostic analysis.
    /// </summary>
    public record DiagnosticIssueDetected(string MachineId, string DiagnosticId, string IssueId, string RuleName, string Severity, string Description) : Phase9BaseEvent;
}
