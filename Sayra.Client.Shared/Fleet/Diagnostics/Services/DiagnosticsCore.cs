using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;
using Sayra.Client.Shared.Fleet.Diagnostics.Domain.Models;
using Sayra.Client.Shared.Fleet.Diagnostics.Interfaces;

namespace Sayra.Client.Shared.Fleet.Diagnostics.Services
{
    /// <summary>
    /// Represents an active diagnostic tracking session.
    /// </summary>
    public class DiagnosticsSession
    {
        public string DiagnosticId { get; } = Guid.NewGuid().ToString();
        public string MachineId { get; }
        public string OperatorId { get; }
        public string CorrelationId { get; }
        public DiagnosticExecutionStatus Status { get; set; } = DiagnosticExecutionStatus.Pending;
        public double ProgressPercentage { get; set; }
        public string CurrentStep { get; set; } = "Initializing";
        public DateTime StartedAtUtc { get; } = DateTime.UtcNow;
        public DateTime? EndedAtUtc { get; set; }
        public List<DiagnosticReport> Reports { get; } = new();
        public DiagnosticResult? Result { get; set; }
        public CancellationTokenSource Cts { get; } = new();

        public DiagnosticsSession(string machineId, string operatorId, string correlationId)
        {
            MachineId = machineId ?? throw new ArgumentNullException(nameof(machineId));
            OperatorId = operatorId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
        }
    }

    /// <summary>
    /// Execution pipeline running independent collectors, isolating failures, and scrubbing sensitive data.
    /// </summary>
    public class DiagnosticsPipeline
    {
        private readonly IEnumerable<IDiagnosticCollector> _collectors;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<DiagnosticsPipeline> _logger;

        private static readonly string[] SensitiveKeywords = { "password", "token", "key", "secret", "credential", "apikey", "connectionstring" };

        public DiagnosticsPipeline(
            IEnumerable<IDiagnosticCollector> collectors,
            IEventDispatcher eventDispatcher,
            ILogger<DiagnosticsPipeline> logger)
        {
            _collectors = collectors ?? throw new ArgumentNullException(nameof(collectors));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes selected collectors under parallel execution boundaries with full failure isolation.
        /// </summary>
        public async Task<List<DiagnosticReport>> ExecuteAsync(
            DiagnosticsSession session,
            IEnumerable<DiagnosticReportType> categories,
            Action<double, string> reportProgress,
            CancellationToken ct)
        {
            _logger.LogInformation("Starting diagnostic execution pipeline for session {Id}...", session.DiagnosticId);

            var targetCollectors = _collectors
                .Where(c => categories.Contains(c.ReportType))
                .ToList();

            if (targetCollectors.Count == 0)
            {
                _logger.LogWarning("No matching collectors found for session {Id}.", session.DiagnosticId);
                return new List<DiagnosticReport>();
            }

            var compiledReports = new ConcurrentBag<DiagnosticReport>();
            double totalSteps = targetCollectors.Count;
            int completedSteps = 0;

            var executionContext = new DiagnosticsExecutionContext
            {
                DiagnosticId = session.DiagnosticId,
                MachineId = session.MachineId,
                CorrelationId = session.CorrelationId,
                OperatorId = session.OperatorId
            };

            // Bounded parallelism (up to 3 parallel collectors)
            using var semaphore = new SemaphoreSlim(3);
            var tasks = targetCollectors.Select(async collector =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    ct.ThrowIfCancellationRequested();
                    _logger.LogDebug("Starting collection step '{Collector}' in session {Id}...", collector.GetType().Name, session.DiagnosticId);

                    var report = await collector.CollectAsync(executionContext, ct);

                    // Scrub sensitive credentials or keys before storing
                    var scrubbedReport = ScrubSensitiveData(report);

                    compiledReports.Add(scrubbedReport);

                    _eventDispatcher.Dispatch(new DiagnosticReportCreated(session.MachineId, session.DiagnosticId, scrubbedReport.ReportId, scrubbedReport.Category));

                    int done = Interlocked.Increment(ref completedSteps);
                    double progress = (done / totalSteps) * 90.0; // reserved 10% for packaging and finalization
                    reportProgress(progress, $"Completed {collector.ReportType} collection.");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Diagnostics session {Id} was cancelled during collector execution.", session.DiagnosticId);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Resilient isolation: Collector '{Collector}' failed in session {Id}.", collector.GetType().Name, session.DiagnosticId);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return compiledReports.ToList();
        }

        private DiagnosticReport ScrubSensitiveData(DiagnosticReport report)
        {
            if (string.IsNullOrWhiteSpace(report.ContentJson)) return report;

            try
            {
                var sections = JsonSerializer.Deserialize<List<DiagnosticSection>>(report.ContentJson);
                if (sections != null)
                {
                    bool modified = false;
                    foreach (var section in sections)
                    {
                        for (int i = 0; i < section.Metrics.Count; i++)
                        {
                            var metric = section.Metrics[i];
                            if (IsSensitive(metric.Name) || IsSensitive(metric.Value))
                            {
                                section.Metrics[i] = metric with { Value = "[REDACTED]" };
                                modified = true;
                            }
                        }
                    }

                    if (modified)
                    {
                        var content = JsonSerializer.Serialize(sections, new JsonSerializerOptions { WriteIndented = true });
                        return report with { ContentJson = content };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse content during sensitive scrubbing.");
            }

            return report;
        }

        private bool IsSensitive(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            return SensitiveKeywords.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Processes analysis results and triggers alerts or audits.
    /// </summary>
    public class DiagnosticsResultProcessor
    {
        private readonly DiagnosticAnalyzer _analyzer;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<DiagnosticsResultProcessor> _logger;

        public DiagnosticsResultProcessor(
            DiagnosticAnalyzer analyzer,
            IEventDispatcher eventDispatcher,
            ILogger<DiagnosticsResultProcessor> logger)
        {
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public DiagnosticResult Process(DiagnosticsSession session, List<DiagnosticReport> reports)
        {
            _logger.LogInformation("Processing and analyzing compiled diagnostic results for session {Id}...", session.DiagnosticId);

            var result = _analyzer.Analyze(session.DiagnosticId, session.MachineId, reports);

            // Audit issues detected and dispatch events
            foreach (var finding in result.Findings)
            {
                if (finding.Severity == "Critical" || finding.Severity == "Emergency")
                {
                    _eventDispatcher.Dispatch(new DiagnosticIssueDetected(
                        session.MachineId,
                        session.DiagnosticId,
                        finding.FindingId,
                        finding.RuleName,
                        finding.Severity,
                        finding.Description
                    ));
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Coordinator managing active and completed stateful diagnostics sessions.
    /// </summary>
    public class DiagnosticsCoordinator
    {
        private readonly DiagnosticsPipeline _pipeline;
        private readonly DiagnosticsResultProcessor _resultProcessor;
        private readonly IDiagnosticPackageBuilder _packageBuilder;
        private readonly IDiagnosticReportRegistry _registry;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<DiagnosticsCoordinator> _logger;

        private readonly ConcurrentDictionary<string, DiagnosticsSession> _sessions = new();

        public DiagnosticsCoordinator(
            DiagnosticsPipeline pipeline,
            DiagnosticsResultProcessor resultProcessor,
            IDiagnosticPackageBuilder packageBuilder,
            IDiagnosticReportRegistry registry,
            IEventDispatcher eventDispatcher,
            ILogger<DiagnosticsCoordinator> logger)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _resultProcessor = resultProcessor ?? throw new ArgumentNullException(nameof(resultProcessor));
            _packageBuilder = packageBuilder ?? throw new ArgumentNullException(nameof(packageBuilder));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts an asynchronous diagnostics gathering run.
        /// </summary>
        public async Task<DiagnosticResult> StartSessionAsync(
            string machineId,
            IEnumerable<DiagnosticReportType> categories,
            string operatorId,
            string correlationId)
        {
            var session = new DiagnosticsSession(machineId, operatorId, correlationId);
            _sessions[session.DiagnosticId] = session;

            _eventDispatcher.Dispatch(new DiagnosticsStarted(machineId, session.DiagnosticId, operatorId));

            session.Status = DiagnosticExecutionStatus.Running;
            session.CurrentStep = "Executing collectors";
            session.ProgressPercentage = 5.0;

            _eventDispatcher.Dispatch(new DiagnosticsProgressChanged(machineId, session.DiagnosticId, session.ProgressPercentage, session.CurrentStep));

            try
            {
                var reports = await _pipeline.ExecuteAsync(
                    session,
                    categories,
                    (p, step) =>
                    {
                        session.ProgressPercentage = p;
                        session.CurrentStep = step;
                        _eventDispatcher.Dispatch(new DiagnosticsProgressChanged(machineId, session.DiagnosticId, p, step));
                    },
                    session.Cts.Token
                );

                session.Cts.Token.ThrowIfCancellationRequested();

                session.ProgressPercentage = 90.0;
                session.CurrentStep = "Analyzing results";
                _eventDispatcher.Dispatch(new DiagnosticsProgressChanged(machineId, session.DiagnosticId, session.ProgressPercentage, session.CurrentStep));

                // Register reports in the registry so package builder can find them
                foreach (var report in reports)
                {
                    _registry.RegisterReport(report);
                    session.Reports.Add(report);
                }

                // Analyze findings
                var result = _resultProcessor.Process(session, reports);

                session.ProgressPercentage = 95.0;
                session.CurrentStep = "Packaging diagnostic archive";
                _eventDispatcher.Dispatch(new DiagnosticsProgressChanged(machineId, session.DiagnosticId, session.ProgressPercentage, session.CurrentStep));

                // Compress package
                var package = await _packageBuilder.BuildPackageAsync(machineId, reports.Select(r => r.ReportId), session.Cts.Token);

                _eventDispatcher.Dispatch(new DiagnosticPackageCreated(machineId, session.DiagnosticId, package.PackageId, package.ArchiveFileName, package.IntegrityHash));

                session.EndedAtUtc = DateTime.UtcNow;
                var finalResult = result with
                {
                    PackagePath = package.ArchiveFileName,
                    EndedAtUtc = DateTime.UtcNow
                };

                session.Result = finalResult;
                session.Status = DiagnosticExecutionStatus.Completed;
                session.ProgressPercentage = 100.0;
                session.CurrentStep = "Completed";

                _eventDispatcher.Dispatch(new DiagnosticsProgressChanged(machineId, session.DiagnosticId, 100.0, "Completed"));
                _eventDispatcher.Dispatch(new DiagnosticsCompleted(machineId, session.DiagnosticId, (long)(session.EndedAtUtc.Value - session.StartedAtUtc).TotalMilliseconds));

                return finalResult;
            }
            catch (OperationCanceledException)
            {
                session.Status = DiagnosticExecutionStatus.Cancelled;
                session.EndedAtUtc = DateTime.UtcNow;
                session.CurrentStep = "Cancelled";
                _eventDispatcher.Dispatch(new DiagnosticsProgressChanged(machineId, session.DiagnosticId, session.ProgressPercentage, "Cancelled"));
                _eventDispatcher.Dispatch(new DiagnosticsFailed(machineId, session.DiagnosticId, "Diagnostics cancelled by the user or due to timeout."));

                var failedResult = new DiagnosticResult
                {
                    DiagnosticId = session.DiagnosticId,
                    MachineId = machineId,
                    IsSuccess = false,
                    ErrorMessage = "Diagnostics execution cancelled.",
                    StartedAtUtc = session.StartedAtUtc,
                    EndedAtUtc = DateTime.UtcNow,
                    OverallStatus = "Cancelled"
                };
                session.Result = failedResult;
                return failedResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run diagnostics session {Id}.", session.DiagnosticId);
                session.Status = DiagnosticExecutionStatus.Failed;
                session.EndedAtUtc = DateTime.UtcNow;
                session.CurrentStep = $"Failed: {ex.Message}";
                _eventDispatcher.Dispatch(new DiagnosticsProgressChanged(machineId, session.DiagnosticId, session.ProgressPercentage, "Failed"));
                _eventDispatcher.Dispatch(new DiagnosticsFailed(machineId, session.DiagnosticId, ex.Message));

                var failedResult = new DiagnosticResult
                {
                    DiagnosticId = session.DiagnosticId,
                    MachineId = machineId,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    StartedAtUtc = session.StartedAtUtc,
                    EndedAtUtc = DateTime.UtcNow,
                    OverallStatus = "Failed"
                };
                session.Result = failedResult;
                return failedResult;
            }
        }

        /// <summary>
        /// Cancels a running diagnostics session.
        /// </summary>
        public bool CancelSession(string diagnosticId)
        {
            if (_sessions.TryGetValue(diagnosticId, out var session))
            {
                if (session.Status == DiagnosticExecutionStatus.Running || session.Status == DiagnosticExecutionStatus.Pending)
                {
                    session.Cts.Cancel();
                    _logger.LogInformation("Cancelled running diagnostics session {Id}.", diagnosticId);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Retrieves the current progress and status of a diagnostics session.
        /// </summary>
        public DiagnosticsSession? GetSession(string diagnosticId)
        {
            _sessions.TryGetValue(diagnosticId, out var session);
            return session;
        }
    }

    /// <summary>
    /// Decoupled diagnostics scheduler supporting execution triggers and schedules.
    /// </summary>
    public class DiagnosticsScheduler
    {
        private readonly DiagnosticsCoordinator _coordinator;
        private readonly ILogger<DiagnosticsScheduler> _logger;

        public DiagnosticsScheduler(DiagnosticsCoordinator coordinator, ILogger<DiagnosticsScheduler> logger)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Registers or schedules a diagnostic execution job.
        /// </summary>
        public void ScheduleJob(string machineId, TimeSpan interval, List<DiagnosticReportType> categories, string operatorId)
        {
            _logger.LogInformation("Scheduling diagnostics checks for workstation {MachineId} every {Interval}...", machineId, interval);
            // In a real scheduler, this would be wired up with a background Timer or System.Threading.Timer
        }
    }

    /// <summary>
    /// Core engine implementing <see cref="IRemoteDiagnosticsService"/> to compile remote workstation diagnostics.
    /// </summary>
    public class RemoteDiagnosticsEngine : IRemoteDiagnosticsService
    {
        private readonly DiagnosticsCoordinator _coordinator;
        private readonly IDiagnosticReportRegistry _registry;
        private readonly ILogger<RemoteDiagnosticsEngine> _logger;

        public RemoteDiagnosticsEngine(
            DiagnosticsCoordinator coordinator,
            IDiagnosticReportRegistry registry,
            ILogger<RemoteDiagnosticsEngine> logger)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<DiagnosticReport> GenerateReportAsync(string machineId, DiagnosticReportType reportType, CancellationToken ct = default)
        {
            _logger.LogInformation("Incoming request to generate diagnostics report '{Type}' for machine {MachineId}...", reportType, machineId);

            // Execute diagnostics session focusing on this specific report type
            var result = await _coordinator.StartSessionAsync(
                machineId,
                new[] { reportType },
                "Administrator",
                Guid.NewGuid().ToString()
            );

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Diagnostics collection failed: {result.ErrorMessage}");
            }

            var report = result.Reports.FirstOrDefault(r => r.Category == reportType);
            if (report == null)
            {
                throw new InvalidOperationException($"No compiled report of type {reportType} was produced.");
            }

            return report;
        }
    }
}
