using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Aggregates environment configurations, hardware specs, update history summaries, and component health into JSON diagnostic payloads.
    /// </summary>
    public class DiagnosticReporter : IDiagnosticReporter
    {
        private readonly IHealthMonitor _healthMonitor;
        private readonly IUpdateHistoryRepository _historyRepository;

        public DiagnosticReporter(IHealthMonitor healthMonitor, IUpdateHistoryRepository historyRepository)
        {
            _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
            _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
        }

        public async Task<string> GenerateDiagnosticReportAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // 1. Gather System Information
                var systemInfo = new Dictionary<string, object>
                {
                    { "MachineName", Environment.MachineName },
                    { "OSVersion", RuntimeInformation.OSDescription },
                    { "OSArchitecture", RuntimeInformation.OSArchitecture.ToString() },
                    { "ProcessArchitecture", RuntimeInformation.ProcessArchitecture.ToString() },
                    { "ProcessorCount", Environment.ProcessorCount },
                    { "DotNetRuntimeVersion", RuntimeInformation.FrameworkDescription },
                    { "TotalPhysicalMemoryBytes", GC.GetGCMemoryInfo().TotalAvailableMemoryBytes }
                };

                // 2. Fetch Health Metric
                var healthMetric = await _healthMonitor.EvaluateHealthAsync(cancellationToken);

                // 3. Summarize Update History
                var allHistory = await _historyRepository.GetAllAsync(cancellationToken);
                var historyRecords = allHistory.ToList();

                int totalCount = historyRecords.Count;
                int successfulCount = historyRecords.Count(r => string.Equals(r.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase));
                int failedCount = historyRecords.Count(r => string.Equals(r.Status, "FAILED", StringComparison.OrdinalIgnoreCase));
                int rolledBackCount = historyRecords.Count(r => string.Equals(r.Status, "ROLLED_BACK", StringComparison.OrdinalIgnoreCase));

                var historySummary = new Dictionary<string, object>
                {
                    { "TotalUpdateAttempts", totalCount },
                    { "SuccessfulCount", successfulCount },
                    { "FailedCount", failedCount },
                    { "RolledBackCount", rolledBackCount }
                };

                // 4. Capture Last Failure and Error Codes
                var lastFailure = historyRecords
                    .Where(r => string.Equals(r.Status, "FAILED", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Status, "ROLLED_BACK", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.InstallationTime)
                    .FirstOrDefault();

                var failureInfo = new Dictionary<string, object>();
                if (lastFailure != null)
                {
                    failureInfo["LastFailureVersion"] = lastFailure.Version;
                    failureInfo["LastFailureTimeUtc"] = lastFailure.InstallationTime.ToString("O");
                    failureInfo["LastFailureErrorCode"] = lastFailure.ErrorCode;
                    failureInfo["LastFailureErrorMessage"] = lastFailure.Result;
                }
                else
                {
                    failureInfo["LastFailureVersion"] = "None";
                    failureInfo["LastFailureErrorCode"] = "None";
                    failureInfo["LastFailureErrorMessage"] = "None";
                }

                // 5. Query active versions
                string currentVersion = "Unknown";
                var latestRecord = historyRecords.OrderByDescending(r => r.InstallationTime).FirstOrDefault();
                if (latestRecord != null)
                {
                    currentVersion = latestRecord.Version;
                }

                // 6. Aggregate final payload
                var report = new Dictionary<string, object>
                {
                    { "CurrentVersion", currentVersion },
                    { "ReportGeneratedAtUtc", DateTime.UtcNow.ToString("O") },
                    { "SystemInformation", systemInfo },
                    { "HealthStatus", new Dictionary<string, object>
                        {
                            { "IsHealthy", healthMetric.IsHealthy },
                            { "LastErrorMessage", healthMetric.LastErrorMessage },
                            { "CheckedAtUtc", healthMetric.CheckedAtUtc.ToString("O") },
                            { "CustomMetrics", healthMetric.CustomMetricsData }
                        }
                    },
                    { "UpdateHistorySummary", historySummary },
                    { "FailureDiagnostics", failureInfo }
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                return JsonSerializer.Serialize(report, options);
            }
            catch (Exception ex)
            {
                throw new DiagnosticReportException("Failed to compile diagnostic report payloads.", ex);
            }
        }
    }
}
