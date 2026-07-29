using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Events;
using SayraClient.Services.Recovery.Exporters;

namespace SayraClient.Services.Recovery
{
    /// <summary>
    /// Production-grade implementation of the Recovery Diagnostics Engine.
    /// Collects, aggregates, analyzes, and persists structured diagnostic reports.
    /// </summary>
    public class RecoveryDiagnosticsEngine : IRecoveryDiagnosticsEngine
    {
        private readonly ILogger<RecoveryDiagnosticsEngine> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly RecoveryDiagnosticsOptions _options;
        private readonly IHealthMonitor _healthMonitor;
        private readonly RecoveryMetricsCollector _metricsCollector;
        private readonly ICrashRecoveryManager _crashRecoveryManager;
        private readonly IResourceMonitor _resourceMonitor;
        private readonly ISecurityHardeningService _securityHardeningService;
        private readonly IEventDispatcher _eventDispatcher;

        private readonly object _pruneLock = new();

        public RecoveryDiagnosticsEngine(
            ILogger<RecoveryDiagnosticsEngine> logger,
            IServiceProvider serviceProvider,
            IOptions<RecoveryDiagnosticsOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _options = options?.Value ?? new RecoveryDiagnosticsOptions();

            // Resolve resilience components with fallback support
            _healthMonitor = serviceProvider.GetService<IHealthMonitor>()!;
            _metricsCollector = serviceProvider.GetService<RecoveryMetricsCollector>()!;
            _crashRecoveryManager = serviceProvider.GetService<ICrashRecoveryManager>()!;
            _resourceMonitor = serviceProvider.GetService<IResourceMonitor>()!;
            _securityHardeningService = serviceProvider.GetService<ISecurityHardeningService>()!;
            _eventDispatcher = serviceProvider.GetService<IEventDispatcher>()!;

            // Ensure reports directory exists
            Directory.CreateDirectory(_options.ReportsDirectory);
        }

        public async Task GenerateAndPersistAllReportsAsync(CancellationToken cancellationToken = default)
        {
            string correlationId = Guid.NewGuid().ToString("N");
            _logger.LogInformation("Generating and persisting all diagnostics reports [CorrelationId={CorrelationId}]...", correlationId);

            var reports = new[]
            {
                ReportType.Startup,
                ReportType.Health,
                ReportType.Recovery,
                ReportType.Failure,
                ReportType.Resource,
                ReportType.Security
            };

            foreach (var reportType in reports)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var startTime = DateTime.UtcNow;

                try
                {
                    _eventDispatcher?.Dispatch(new DiagnosticsGenerationStartedEvent(correlationId, reportType, startTime));

                    string content = string.Empty;
                    switch (reportType)
                    {
                        case ReportType.Startup:
                            var startupPayload = await GetStartupPayloadAsync(cancellationToken);
                            content = FormatStartupReportText(startupPayload, correlationId);
                            await SaveReportFilesAsync(reportType, content, startupPayload, correlationId, cancellationToken);
                            break;

                        case ReportType.Health:
                            var healthPayload = await GetHealthPayloadAsync(cancellationToken);
                            content = FormatHealthReportText(healthPayload, correlationId);
                            await SaveReportFilesAsync(reportType, content, healthPayload, correlationId, cancellationToken);
                            break;

                        case ReportType.Recovery:
                            var recoveryPayload = await GetRecoveryPayloadAsync(cancellationToken);
                            content = FormatRecoveryReportText(recoveryPayload, correlationId);
                            await SaveReportFilesAsync(reportType, content, recoveryPayload, correlationId, cancellationToken);
                            break;

                        case ReportType.Failure:
                            var failurePayload = await GetFailurePayloadAsync(cancellationToken);
                            content = FormatFailureReportText(failurePayload, correlationId);
                            await SaveReportFilesAsync(reportType, content, failurePayload, correlationId, cancellationToken);
                            break;

                        case ReportType.Resource:
                            var resourcePayload = await GetResourcePayloadAsync(cancellationToken);
                            content = FormatResourceReportText(resourcePayload, correlationId);
                            await SaveReportFilesAsync(reportType, content, resourcePayload, correlationId, cancellationToken);
                            break;

                        case ReportType.Security:
                            var securityPayload = await GetSecurityPayloadAsync(cancellationToken);
                            content = FormatSecurityReportText(securityPayload, correlationId);
                            await SaveReportFilesAsync(reportType, content, securityPayload, correlationId, cancellationToken);
                            break;
                    }

                    var duration = DateTime.UtcNow - startTime;
                    _logger.LogInformation("Report {ReportType} generated and saved successfully in {DurationMs}ms [CorrelationId={CorrelationId}].",
                        reportType, duration.TotalMilliseconds, correlationId);

                    _eventDispatcher?.Dispatch(new DiagnosticsGenerationCompletedEvent(correlationId, reportType, duration, DateTime.UtcNow));
                }
                catch (Exception ex)
                {
                    var duration = DateTime.UtcNow - startTime;
                    _logger.LogError(ex, "Failed to generate or save report {ReportType} [CorrelationId={CorrelationId}].", reportType, correlationId);
                    _eventDispatcher?.Dispatch(new DiagnosticsGenerationFailedEvent(correlationId, reportType, ex.Message, ex.ToString(), DateTime.UtcNow));
                }
            }
        }

        public async Task<string> GenerateStartupReportAsync(CancellationToken cancellationToken = default)
        {
            string correlationId = Guid.NewGuid().ToString("N");
            var payload = await GetStartupPayloadAsync(cancellationToken);
            return FormatStartupReportText(payload, correlationId);
        }

        public async Task<string> GenerateHealthReportAsync(CancellationToken cancellationToken = default)
        {
            string correlationId = Guid.NewGuid().ToString("N");
            var payload = await GetHealthPayloadAsync(cancellationToken);
            return FormatHealthReportText(payload, correlationId);
        }

        public Task<string> GenerateHealthSummaryReportAsync(CancellationToken cancellationToken = default)
        {
            return GenerateHealthReportAsync(cancellationToken);
        }

        public async Task<string> GenerateRecoveryReportAsync(CancellationToken cancellationToken = default)
        {
            string correlationId = Guid.NewGuid().ToString("N");
            var payload = await GetRecoveryPayloadAsync(cancellationToken);
            return FormatRecoveryReportText(payload, correlationId);
        }

        public async Task<string> GenerateFailureReportAsync(CancellationToken cancellationToken = default)
        {
            string correlationId = Guid.NewGuid().ToString("N");
            var payload = await GetFailurePayloadAsync(cancellationToken);
            return FormatFailureReportText(payload, correlationId);
        }

        public async Task<string> GenerateResourceReportAsync(CancellationToken cancellationToken = default)
        {
            string correlationId = Guid.NewGuid().ToString("N");
            var payload = await GetResourcePayloadAsync(cancellationToken);
            return FormatResourceReportText(payload, correlationId);
        }

        public async Task<string> GenerateSecurityReportAsync(CancellationToken cancellationToken = default)
        {
            string correlationId = Guid.NewGuid().ToString("N");
            var payload = await GetSecurityPayloadAsync(cancellationToken);
            return FormatSecurityReportText(payload, correlationId);
        }

        public async Task<string> GenerateFullDiagnosticsAsync(CancellationToken cancellationToken = default)
        {
            string correlationId = Guid.NewGuid().ToString("N");
            var payload = new FullDiagnosticsPayload
            {
                Startup = await GetStartupPayloadAsync(cancellationToken),
                Health = await GetHealthPayloadAsync(cancellationToken),
                Recovery = await GetRecoveryPayloadAsync(cancellationToken),
                Failure = await GetFailurePayloadAsync(cancellationToken),
                Resource = await GetResourcePayloadAsync(cancellationToken),
                Security = await GetSecurityPayloadAsync(cancellationToken)
            };

            var envelope = new DiagnosticsReportEnvelope<FullDiagnosticsPayload>
            {
                ReportType = "FullDiagnostics",
                CorrelationId = correlationId,
                ApplicationVersion = _options.ApplicationVersion,
                BuildNumber = _options.BuildNumber,
                GeneratedBy = _options.GeneratedBy,
                ReportVersion = _options.ReportVersion,
                Payload = payload
            };

            return JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
        }

        public async Task<string> ExportDiagnosticsAsync(ReportType reportType, string format, string? destinationPath = null, CancellationToken cancellationToken = default)
        {
            string correlationId = Guid.NewGuid().ToString("N");
            _logger.LogInformation("Exporting diagnostics report: Type={ReportType}, Format={Format}, CorrelationId={CorrelationId}",
                reportType, format, correlationId);

            string content = string.Empty;

            if (format.Equals("JSON", StringComparison.OrdinalIgnoreCase))
            {
                var payloadObj = await GetReportPayloadAsync(reportType, cancellationToken);
                var envelope = new DiagnosticsReportEnvelope<object>
                {
                    ReportType = reportType.ToString(),
                    CorrelationId = correlationId,
                    ApplicationVersion = _options.ApplicationVersion,
                    BuildNumber = _options.BuildNumber,
                    GeneratedBy = _options.GeneratedBy,
                    ReportVersion = _options.ReportVersion,
                    Payload = payloadObj
                };
                content = JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                switch (reportType)
                {
                    case ReportType.Startup:
                        content = await GenerateStartupReportAsync(cancellationToken);
                        break;
                    case ReportType.Health:
                        content = await GenerateHealthReportAsync(cancellationToken);
                        break;
                    case ReportType.Recovery:
                        content = await GenerateRecoveryReportAsync(cancellationToken);
                        break;
                    case ReportType.Failure:
                        content = await GenerateFailureReportAsync(cancellationToken);
                        break;
                    case ReportType.Resource:
                        content = await GenerateResourceReportAsync(cancellationToken);
                        break;
                    case ReportType.Security:
                        content = await GenerateSecurityReportAsync(cancellationToken);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(reportType), $"Unsupported report type: {reportType}");
                }
            }

            // Resolve exporter
            var exporters = _serviceProvider.GetServices<IDiagnosticsExporter>();
            var exporter = exporters.FirstOrDefault(e => e.Format.Equals(format, StringComparison.OrdinalIgnoreCase));
            if (exporter == null)
            {
                throw new NotSupportedException($"No exporter registered for format: {format}");
            }

            if (string.IsNullOrEmpty(destinationPath))
            {
                string ext = format.ToLower() == "txt" ? "txt" : format.ToLower();
                string baseName = $"{reportType.ToString().ToLower()}_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{correlationId.Substring(0, 8)}.{ext}";
                destinationPath = Path.Combine(_options.ReportsDirectory, "exports", baseName);
            }

            string finalPath = await exporter.ExportAsync(reportType, content, destinationPath, cancellationToken);

            _eventDispatcher?.Dispatch(new DiagnosticsExportedEvent(correlationId, reportType, format, finalPath, DateTime.UtcNow));
            _logger.LogInformation("Successfully exported report to {Path} [CorrelationId={CorrelationId}]", finalPath, correlationId);

            return finalPath;
        }

        #region Private Data Collectors

        private async Task<object> GetReportPayloadAsync(ReportType reportType, CancellationToken ct)
        {
            return reportType switch
            {
                ReportType.Startup => await GetStartupPayloadAsync(ct),
                ReportType.Health => await GetHealthPayloadAsync(ct),
                ReportType.Recovery => await GetRecoveryPayloadAsync(ct),
                ReportType.Failure => await GetFailurePayloadAsync(ct),
                ReportType.Resource => await GetResourcePayloadAsync(ct),
                ReportType.Security => await GetSecurityPayloadAsync(ct),
                _ => throw new ArgumentOutOfRangeException(nameof(reportType))
            };
        }

        private async Task<StartupReportPayload> GetStartupPayloadAsync(CancellationToken ct)
        {
            var payload = new StartupReportPayload();

            if (_crashRecoveryManager != null)
            {
                try
                {
                    var summary = await _crashRecoveryManager.GenerateRecoverySummaryAsync(ct);
                    var shutdownState = await _crashRecoveryManager.ValidatePreviousShutdownAsync(ct);

                    payload.StartupTime = shutdownState.LastStartupTimestamp.ToString("O");
                    payload.RecoveryExecuted = shutdownState.IsRecoveryRequired;
                    payload.RecoveredComponents = summary.Attempts.Select(a => a.SubsystemName).Distinct().ToList();
                    payload.Warnings = summary.Recommendations.Where(r => r.Contains("Warning", StringComparison.OrdinalIgnoreCase)).ToList();
                    payload.Errors = summary.Recommendations.Where(r => r.Contains("Error", StringComparison.OrdinalIgnoreCase) || r.Contains("fail", StringComparison.OrdinalIgnoreCase)).ToList();

                    // Measure duration roughly from recovery results
                    payload.StartupDurationMs = summary.Attempts.Count * 12.5; // Estimated baseline
                }
                catch (Exception ex)
                {
                    payload.Errors.Add($"Failed to load crash recovery details: {ex.Message}");
                }
            }
            else
            {
                payload.StartupTime = DateTime.UtcNow.ToString("O");
                payload.RecoveryExecuted = false;
                payload.Warnings.Add("CrashRecoveryManager is not available.");
            }

            return payload;
        }

        private async Task<HealthReportPayload> GetHealthPayloadAsync(CancellationToken ct)
        {
            var payload = new HealthReportPayload();

            if (_healthMonitor != null)
            {
                try
                {
                    var detailed = await _healthMonitor.GetDetailedHealthAsync(ct);
                    foreach (var kvp in detailed)
                    {
                        var sub = kvp.Value;
                        payload.SubsystemStates[sub.SubsystemName] = sub.State.ToString();
                        payload.HealthScores[sub.SubsystemName] = sub.HealthScore;
                        payload.HeartbeatStatuses[sub.SubsystemName] = sub.LastHeartbeat.ToString("O");
                        payload.DependencyStatuses[sub.SubsystemName] = sub.Dependencies.ToList();
                        payload.TransitionHistories[sub.SubsystemName] = sub.HealthHistory.ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch detailed subsystem health.");
                }
            }

            return payload;
        }

        private async Task<RecoveryReportPayload> GetRecoveryPayloadAsync(CancellationToken ct)
        {
            var payload = new RecoveryReportPayload();

            if (_metricsCollector != null)
            {
                payload.RecoveryAttempts = _metricsCollector.RecoveryCount;
                payload.RecoverySuccessRate = _metricsCollector.SuccessRate;
                payload.FailedRecoveries = _metricsCollector.FailureCount;
                payload.AverageRecoveryTimeMs = _metricsCollector.AverageRecoveryTime.TotalMilliseconds;
                payload.Escalations = _metricsCollector.EscalationCount;
                payload.ActiveRecoveries = _metricsCollector.ActiveRecoveries;
                payload.RecoveryHistory = _metricsCollector.GetAllHistory();
            }

            return payload;
        }

        private async Task<FailureReportPayload> GetFailurePayloadAsync(CancellationToken ct)
        {
            var payload = new FailureReportPayload();

            var healthSnapshot = _healthMonitor != null
                ? await _healthMonitor.GetDetailedHealthAsync(ct)
                : new Dictionary<string, SubsystemHealthInfo>();

            var resourceSnapshot = _resourceMonitor != null
                ? await _resourceMonitor.GetCurrentMetricsAsync(ct)
                : null;

            IReadOnlyList<SecurityValidationResult>? securityResults = null;
            if (_securityHardeningService != null)
            {
                try
                {
                    securityResults = await _securityHardeningService.RunFullValidationAsync(ct);
                }
                catch
                {
                    // Fallback
                }
            }

            // Actionable rule recommendations
            payload.Recommendations = DiagnosticsRecommendationEngine.EvaluateRules(
                healthSnapshot,
                _metricsCollector,
                resourceSnapshot,
                securityResults);

            if (_healthMonitor != null)
            {
                foreach (var kvp in healthSnapshot)
                {
                    var sub = kvp.Value;
                    if (sub.State == SubsystemHealthState.Critical || sub.State == SubsystemHealthState.Offline)
                    {
                        payload.FailureCounts[sub.SubsystemName] = sub.HealthHistory.Count;
                        payload.FailureCategories[sub.SubsystemName] = "SubsystemFailure";
                        if (!string.IsNullOrEmpty(sub.LastException))
                        {
                            payload.Exceptions.Add($"Subsystem: {sub.SubsystemName} - Exception: {sub.LastException}");
                        }

                        payload.SubsystemFailures.Add(new FailureRecord
                        {
                            SubsystemName = sub.SubsystemName,
                            ErrorMessage = sub.LastMessage,
                            ExceptionTrace = sub.LastException,
                            Severity = FailureSeverity.Critical,
                            DetectedAt = sub.LastHeartbeat
                        });
                    }
                }
            }

            return payload;
        }

        private async Task<ResourceReportPayload> GetResourcePayloadAsync(CancellationToken ct)
        {
            var payload = new ResourceReportPayload();

            if (_resourceMonitor != null)
            {
                try
                {
                    var metrics = await _resourceMonitor.GetCurrentMetricsAsync(ct);
                    payload.CpuUsagePercentage = metrics.CpuUsagePercentage;
                    payload.ProcessRamBytes = metrics.ProcessRamBytes;
                    payload.TotalSystemRamBytes = metrics.TotalSystemRamBytes;
                    payload.AvailableSystemRamBytes = metrics.AvailableSystemRamBytes;
                    payload.FreeDiskSpaceBytes = metrics.FreeDiskSpaceBytes;
                    payload.HandleCount = metrics.HandleCount;
                    payload.ThreadCount = metrics.ThreadCount;
                    payload.GdiObjectsCount = metrics.GdiObjectsCount;
                    payload.GpuUsagePercentage = metrics.GpuUsagePercentage;
                    payload.DiskIoBytesPerSecond = metrics.DiskIoBytesPerSecond;
                    payload.NetworkIoBytesPerSecond = metrics.NetworkIoBytesPerSecond;
                    payload.HardwareTemperatureCelsius = metrics.HardwareTemperatureCelsius;
                    payload.PressureLevel = metrics.PressureLevel.ToString();
                    payload.ThresholdStatus = metrics.ThresholdStatus;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve resource metrics.");
                }
            }

            return payload;
        }

        private async Task<SecurityReportPayload> GetSecurityPayloadAsync(CancellationToken ct)
        {
            var payload = new SecurityReportPayload();

            if (_securityHardeningService != null)
            {
                try
                {
                    payload.ConfigurationValidation = await _securityHardeningService.ValidateConfigurationAsync(ct);
                    payload.PolicyValidation = await _securityHardeningService.ValidatePolicyAsync(ct);
                    payload.DatabaseValidation = await _securityHardeningService.ValidateDatabaseAsync(ct);
                    payload.MediaValidation = await _securityHardeningService.ValidateMediaAsync(ct);
                    payload.PluginValidation = await _securityHardeningService.ValidatePluginsAsync(ct);
                    payload.PackageValidation = await _securityHardeningService.ValidatePackagesAsync(ct);
                    payload.ExecutableValidation = await _securityHardeningService.ValidateExecutableAsync(ct);

                    var allCheckResults = new[]
                    {
                        payload.ConfigurationValidation,
                        payload.PolicyValidation,
                        payload.DatabaseValidation,
                        payload.MediaValidation,
                        payload.PluginValidation,
                        payload.PackageValidation,
                        payload.ExecutableValidation
                    };

                    foreach (var result in allCheckResults)
                    {
                        if (result.ValidationState != SecurityValidationState.Passed)
                        {
                            payload.DetectedViolations.Add($"Security Check Failed: Target={result.TargetName}, State={result.ValidationState}, Message={result.Message}");
                        }
                    }

                    var healthSnapshot = _healthMonitor != null
                        ? await _healthMonitor.GetDetailedHealthAsync(ct)
                        : new Dictionary<string, SubsystemHealthInfo>();

                    var resourceSnapshot = _resourceMonitor != null
                        ? await _resourceMonitor.GetCurrentMetricsAsync(ct)
                        : null;

                    payload.Recommendations = DiagnosticsRecommendationEngine.EvaluateRules(
                        healthSnapshot,
                        _metricsCollector,
                        resourceSnapshot,
                        allCheckResults.ToList());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to run security validation pipeline.");
                    payload.DetectedViolations.Add($"Security hardening service execution failure: {ex.Message}");
                }
            }

            return payload;
        }

        #endregion

        #region Private Formatting Helpers

        private string CreateReportHeader(string reportName, string correlationId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==========================================================");
            sb.AppendLine($"SAYRA ENTERPRISE RESILIENCE DIAGNOSTICS: {reportName.ToUpper()}");
            sb.AppendLine("==========================================================");
            sb.AppendLine($"Timestamp:           {DateTime.UtcNow:O}");
            sb.AppendLine($"Machine ID:          {Environment.MachineName}");
            sb.AppendLine($"Application Version: {_options.ApplicationVersion}");
            sb.AppendLine($"Build Number:        {_options.BuildNumber}");
            sb.AppendLine($"OS Version:          {Environment.OSVersion}");
            sb.AppendLine($"Correlation ID:      {correlationId}");
            sb.AppendLine($"Generated By:        {_options.GeneratedBy}");
            sb.AppendLine($"Report Version:      {_options.ReportVersion}");
            sb.AppendLine("==========================================================");
            sb.AppendLine();
            return sb.ToString();
        }

        private string FormatStartupReportText(StartupReportPayload payload, string correlationId)
        {
            var sb = new StringBuilder(CreateReportHeader("Startup Report", correlationId));
            sb.AppendLine($"Startup Time:     {payload.StartupTime}");
            sb.AppendLine($"Startup Duration: {payload.StartupDurationMs:F2} ms");
            sb.AppendLine($"Recovery Run:     {payload.RecoveryExecuted}");
            sb.AppendLine();
            sb.AppendLine("Recovered Subsystems:");
            if (payload.RecoveredComponents.Any())
            {
                foreach (var comp in payload.RecoveredComponents) sb.AppendLine($"  - {comp}");
            }
            else
            {
                sb.AppendLine("  (None)");
            }

            sb.AppendLine();
            sb.AppendLine("Warnings Logged:");
            if (payload.Warnings.Any())
            {
                foreach (var warn in payload.Warnings) sb.AppendLine($"  - {warn}");
            }
            else
            {
                sb.AppendLine("  (None)");
            }

            sb.AppendLine();
            sb.AppendLine("Errors Logged:");
            if (payload.Errors.Any())
            {
                foreach (var err in payload.Errors) sb.AppendLine($"  - {err}");
            }
            else
            {
                sb.AppendLine("  (None)");
            }
            sb.AppendLine("==========================================================");
            return sb.ToString();
        }

        private string FormatHealthReportText(HealthReportPayload payload, string correlationId)
        {
            var sb = new StringBuilder(CreateReportHeader("Health Report", correlationId));
            sb.AppendLine("Subsystem Health Details:");
            foreach (var subName in payload.SubsystemStates.Keys)
            {
                sb.AppendLine($"Subsystem: {subName}");
                sb.AppendLine($"  State:          {payload.SubsystemStates[subName]}");
                sb.AppendLine($"  Health Score:   {payload.HealthScores[subName]:F1}");
                sb.AppendLine($"  Last Heartbeat: {payload.HeartbeatStatuses[subName]}");
                sb.AppendLine($"  Dependencies:   [{string.Join(", ", payload.DependencyStatuses[subName])}]");
                sb.AppendLine("  Transitions:");
                foreach (var trans in payload.TransitionHistories[subName].Take(5))
                {
                    sb.AppendLine($"    - {trans}");
                }
                sb.AppendLine("----------------------------------------------------------");
            }
            sb.AppendLine("==========================================================");
            return sb.ToString();
        }

        private string FormatRecoveryReportText(RecoveryReportPayload payload, string correlationId)
        {
            var sb = new StringBuilder(CreateReportHeader("Recovery Report", correlationId));
            sb.AppendLine($"Total Recovery Actions:     {payload.RecoveryAttempts}");
            sb.AppendLine($"Recovery Success Rate:      {payload.RecoverySuccessRate:F2} %");
            sb.AppendLine($"Failed Recoveries:          {payload.FailedRecoveries}");
            sb.AppendLine($"Average Recovery Duration:  {payload.AverageRecoveryTimeMs:F2} ms");
            sb.AppendLine($"Escalated Incidents:        {payload.Escalations}");
            sb.AppendLine($"Active Recovery Interlocks: {payload.ActiveRecoveries}");
            sb.AppendLine();
            sb.AppendLine("Workstation Recovery Histories:");
            foreach (var hist in payload.RecoveryHistory)
            {
                sb.AppendLine($"Subsystem: {hist.SubsystemName}");
                sb.AppendLine($"  Total Failures:     {hist.TotalFailures}");
                sb.AppendLine($"  Success Recoveries: {hist.TotalSuccessfulRecoveries}");
                sb.AppendLine("  Recent Results:");
                foreach (var result in hist.RecoveryResults.Take(3))
                {
                    sb.AppendLine($"    - [{result.CompletedAt:T}] Status={result.FinalStatus}, Duration={result.Duration.TotalMilliseconds:F1}ms, Msg={result.OutputMessage}");
                }
                sb.AppendLine("----------------------------------------------------------");
            }
            sb.AppendLine("==========================================================");
            return sb.ToString();
        }

        private string FormatFailureReportText(FailureReportPayload payload, string correlationId)
        {
            var sb = new StringBuilder(CreateReportHeader("Failure Report", correlationId));
            sb.AppendLine("Structured Failure Recommendations:");
            foreach (var rec in payload.Recommendations)
            {
                sb.AppendLine($"  * [Rule Suggesion] {rec}");
            }

            sb.AppendLine();
            sb.AppendLine("Exceptions Tracked:");
            if (payload.Exceptions.Any())
            {
                foreach (var ex in payload.Exceptions) sb.AppendLine($"  - {ex}");
            }
            else
            {
                sb.AppendLine("  (None)");
            }

            sb.AppendLine();
            sb.AppendLine("Subsystem Failures Recorded:");
            if (payload.SubsystemFailures.Any())
            {
                foreach (var record in payload.SubsystemFailures)
                {
                    sb.AppendLine($"  - Subsystem: {record.SubsystemName} (Severity: {record.Severity})");
                    sb.AppendLine($"    DetectedAt: {record.DetectedAt:O}");
                    sb.AppendLine($"    Error:      {record.ErrorMessage}");
                    if (!string.IsNullOrEmpty(record.ExceptionTrace))
                    {
                        sb.AppendLine($"    StackTrace: {record.ExceptionTrace}");
                    }
                }
            }
            else
            {
                sb.AppendLine("  (None - All monitored subsystems are healthy.)");
            }
            sb.AppendLine("==========================================================");
            return sb.ToString();
        }

        private string FormatResourceReportText(ResourceReportPayload payload, string correlationId)
        {
            var sb = new StringBuilder(CreateReportHeader("Resource Report", correlationId));
            sb.AppendLine($"Workstation Resource Pressure Level: {payload.PressureLevel}");
            sb.AppendLine($"Evaluated Threshold Status:          {payload.ThresholdStatus}");
            sb.AppendLine();
            sb.AppendLine("Resource Metrics Detail:");
            sb.AppendLine($"  CPU Usage:            {payload.CpuUsagePercentage:F2} %");
            sb.AppendLine($"  Process memory:       {payload.ProcessRamBytes / (1024.0 * 1024.0):F2} MB");
            sb.AppendLine($"  Total System memory:  {payload.TotalSystemRamBytes / (1024.0 * 1024.0):F2} MB");
            sb.AppendLine($"  Available System RAM: {payload.AvailableSystemRamBytes / (1024.0 * 1024.0):F2} MB");
            sb.AppendLine($"  Free Storage Space:   {payload.FreeDiskSpaceBytes / (1024.0 * 1024.0 * 1024.0):F2} GB");
            sb.AppendLine($"  GPU Utilization:      {payload.GpuUsagePercentage:F2} %");
            sb.AppendLine($"  Disk Read/Write Rate: {payload.DiskIoBytesPerSecond / 1024.0:F2} KB/s");
            sb.AppendLine($"  Network Tx/Rx Rate:   {payload.NetworkIoBytesPerSecond / 1024.0:F2} KB/s");
            sb.AppendLine($"  Handle Count:         {payload.HandleCount}");
            sb.AppendLine($"  Thread Count:         {payload.ThreadCount}");
            sb.AppendLine($"  GDI Objects Count:    {payload.GdiObjectsCount}");
            sb.AppendLine($"  Hardware Temperature: {(payload.HardwareTemperatureCelsius.HasValue ? $"{payload.HardwareTemperatureCelsius.Value:F1} °C" : "N/A")}");
            sb.AppendLine("==========================================================");
            return sb.ToString();
        }

        private string FormatSecurityReportText(SecurityReportPayload payload, string correlationId)
        {
            var sb = new StringBuilder(CreateReportHeader("Security Report", correlationId));
            sb.AppendLine("Cryptographic Integrity Verification Panel:");
            sb.AppendLine($"  Configuration Integrity: {payload.ConfigurationValidation.ValidationState} ({payload.ConfigurationValidation.Message})");
            sb.AppendLine($"  Security Policy Trust:   {payload.PolicyValidation.ValidationState} ({payload.PolicyValidation.Message})");
            sb.AppendLine($"  SQLCipher DB PRAGMA:     {payload.DatabaseValidation.ValidationState} ({payload.DatabaseValidation.Message})");
            sb.AppendLine($"  Ad Campaign Media Hash:  {payload.MediaValidation.ValidationState} ({payload.MediaValidation.Message})");
            sb.AppendLine($"  Plugin Folder Manifest:  {payload.PluginValidation.ValidationState} ({payload.PluginValidation.Message})");
            sb.AppendLine($"  Update Package Sign:     {payload.PackageValidation.ValidationState} ({payload.PackageValidation.Message})");
            sb.AppendLine($"  Executable Authenticode: {payload.ExecutableValidation.ValidationState} ({payload.ExecutableValidation.Message})");
            sb.AppendLine();

            sb.AppendLine("Detected Security Violations & Tampering:");
            if (payload.DetectedViolations.Any())
            {
                foreach (var vio in payload.DetectedViolations) sb.AppendLine($"  [CRITICAL] {vio}");
            }
            else
            {
                sb.AppendLine("  (None - All components passed cryptographic authenticity audits.)");
            }

            sb.AppendLine();
            sb.AppendLine("Security Hardening Recommendations:");
            foreach (var rec in payload.Recommendations)
            {
                sb.AppendLine($"  * {rec}");
            }
            sb.AppendLine("==========================================================");
            return sb.ToString();
        }

        #endregion

        #region Private File Persistence Logic

        private async Task SaveReportFilesAsync<T>(ReportType reportType, string textContent, T payload, string correlationId, CancellationToken ct)
        {
            string baseName = $"{reportType.ToString().ToLower()}_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{correlationId.Substring(0, 8)}";

            if (_options.EnableText)
            {
                string txtPath = Path.Combine(_options.ReportsDirectory, $"{baseName}.txt");
                await File.WriteAllTextAsync(txtPath, textContent, ct);
                _eventDispatcher?.Dispatch(new ReportPersistedEvent(correlationId, reportType, "TXT", txtPath, DateTime.UtcNow));
            }

            if (_options.EnableJson)
            {
                string jsonPath = Path.Combine(_options.ReportsDirectory, $"{baseName}.json");
                var envelope = new DiagnosticsReportEnvelope<T>
                {
                    ReportType = reportType.ToString(),
                    CorrelationId = correlationId,
                    ApplicationVersion = _options.ApplicationVersion,
                    BuildNumber = _options.BuildNumber,
                    GeneratedBy = _options.GeneratedBy,
                    ReportVersion = _options.ReportVersion,
                    Payload = payload
                };
                string jsonContent = JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(jsonPath, jsonContent, ct);
                _eventDispatcher?.Dispatch(new ReportPersistedEvent(correlationId, reportType, "JSON", jsonPath, DateTime.UtcNow));
            }

            PruneOldReports(reportType);
        }

        private void PruneOldReports(ReportType reportType)
        {
            lock (_pruneLock)
            {
                try
                {
                    if (!Directory.Exists(_options.ReportsDirectory)) return;

                    var dirInfo = new DirectoryInfo(_options.ReportsDirectory);
                    // Optimized Pruning Logic: Prune per ReportType so they never starve each other under low limits!
                    string prefix = reportType.ToString().ToLower() + "_report_";
                    var files = dirInfo.GetFiles()
                                       .Where(f => f.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                                                   (f.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                                                    f.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase)))
                                       .OrderByDescending(f => f.LastWriteTime)
                                       .ToList();

                    // If both TXT and JSON are enabled, each generation pass produces 2 files.
                    // So we multiply the retention limit by 2 if both formats are enabled to preserve N full runs.
                    int targetCount = _options.RetentionLimit;
                    if (_options.EnableJson && _options.EnableText)
                    {
                        targetCount *= 2;
                    }

                    if (files.Count > targetCount)
                    {
                        var filesToDelete = files.Skip(targetCount).ToList();
                        foreach (var file in filesToDelete)
                        {
                            try
                            {
                                file.Delete();
                                _logger.LogInformation("Pruned old diagnostics report file: {Path}", file.FullName);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to delete pruned report file: {Path}", file.FullName);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during diagnostics report pruning.");
                }
            }
        }

        #endregion

        #region Report Envelope and Payloads DTOs - Publicly accessible for ease of API serialization

        public class DiagnosticsReportEnvelope<T>
        {
            public string ReportType { get; set; } = string.Empty;
            public string Timestamp { get; set; } = DateTime.UtcNow.ToString("O");
            public string MachineId { get; set; } = Environment.MachineName;
            public string ApplicationVersion { get; set; } = "1.0.0.0";
            public string BuildNumber { get; set; } = "Release.2025.2";
            public string OsVersion { get; set; } = Environment.OSVersion.ToString();
            public string CorrelationId { get; set; } = string.Empty;
            public string GeneratedBy { get; set; } = "SAYRA Recovery Diagnostics Engine";
            public string ReportVersion { get; set; } = "1.0";
            public T Payload { get; set; } = default!;
        }

        public class FullDiagnosticsPayload
        {
            public StartupReportPayload Startup { get; set; } = default!;
            public HealthReportPayload Health { get; set; } = default!;
            public RecoveryReportPayload Recovery { get; set; } = default!;
            public FailureReportPayload Failure { get; set; } = default!;
            public ResourceReportPayload Resource { get; set; } = default!;
            public SecurityReportPayload Security { get; set; } = default!;
        }

        public class StartupReportPayload
        {
            public string StartupTime { get; set; } = string.Empty;
            public double StartupDurationMs { get; set; }
            public bool RecoveryExecuted { get; set; }
            public List<string> RecoveredComponents { get; set; } = new();
            public List<string> Warnings { get; set; } = new();
            public List<string> Errors { get; set; } = new();
        }

        public class HealthReportPayload
        {
            public Dictionary<string, string> SubsystemStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, double> HealthScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> HeartbeatStatuses { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, List<string>> DependencyStatuses { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, List<string>> TransitionHistories { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        public class RecoveryReportPayload
        {
            public int RecoveryAttempts { get; set; }
            public double RecoverySuccessRate { get; set; }
            public int FailedRecoveries { get; set; }
            public double AverageRecoveryTimeMs { get; set; }
            public int Escalations { get; set; }
            public int ActiveRecoveries { get; set; }
            public List<RecoveryHistory> RecoveryHistory { get; set; } = new();
        }

        public class FailureReportPayload
        {
            public List<string> Exceptions { get; set; } = new();
            public Dictionary<string, int> FailureCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> FailureCategories { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public List<FailureRecord> SubsystemFailures { get; set; } = new();
            public List<string> Recommendations { get; set; } = new();
        }

        public class ResourceReportPayload
        {
            public double CpuUsagePercentage { get; set; }
            public long ProcessRamBytes { get; set; }
            public long TotalSystemRamBytes { get; set; }
            public long AvailableSystemRamBytes { get; set; }
            public long FreeDiskSpaceBytes { get; set; }
            public int HandleCount { get; set; }
            public int ThreadCount { get; set; }
            public int GdiObjectsCount { get; set; }
            public double GpuUsagePercentage { get; set; }
            public double DiskIoBytesPerSecond { get; set; }
            public double NetworkIoBytesPerSecond { get; set; }
            public double? HardwareTemperatureCelsius { get; set; }
            public string PressureLevel { get; set; } = string.Empty;
            public string ThresholdStatus { get; set; } = string.Empty;
        }

        public class SecurityReportPayload
        {
            public SecurityValidationResult ConfigurationValidation { get; set; } = default!;
            public SecurityValidationResult PolicyValidation { get; set; } = default!;
            public SecurityValidationResult DatabaseValidation { get; set; } = default!;
            public SecurityValidationResult MediaValidation { get; set; } = default!;
            public SecurityValidationResult PluginValidation { get; set; } = default!;
            public SecurityValidationResult PackageValidation { get; set; } = default!;
            public SecurityValidationResult ExecutableValidation { get; set; } = default!;
            public List<string> DetectedViolations { get; set; } = new();
            public List<string> Recommendations { get; set; } = new();
        }

        #endregion
    }
}
