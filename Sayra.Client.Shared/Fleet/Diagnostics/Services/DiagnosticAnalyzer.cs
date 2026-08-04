using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Fleet.Diagnostics.Domain.Models;

namespace Sayra.Client.Shared.Fleet.Diagnostics.Services
{
    /// <summary>
    /// Rule-based diagnostic analyzer that parses compiled reports, calculates health scores,
    /// prioritizes anomalies, and generates actionable recommendations.
    /// </summary>
    public class DiagnosticAnalyzer
    {
        private readonly ILogger<DiagnosticAnalyzer> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticAnalyzer"/> class.
        /// </summary>
        public DiagnosticAnalyzer(ILogger<DiagnosticAnalyzer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Analyzes a set of compiled diagnostic reports, generating a unified analysis result.
        /// </summary>
        public DiagnosticResult Analyze(string diagnosticId, string machineId, List<DiagnosticReport> reports)
        {
            if (string.IsNullOrWhiteSpace(diagnosticId)) throw new ArgumentException("Diagnostic ID cannot be null or empty.", nameof(diagnosticId));
            if (string.IsNullOrWhiteSpace(machineId)) throw new ArgumentException("Machine ID cannot be null or empty.", nameof(machineId));
            if (reports == null) throw new ArgumentNullException(nameof(reports));

            _logger.LogInformation("Analyzing {Count} diagnostic report(s) for machine {MachineId}...", reports.Count, machineId);

            var findings = new List<DiagnosticFinding>();
            var recommendations = new List<DiagnosticRecommendation>();

            // Parse metrics and findings from report ContentJson
            foreach (var report in reports)
            {
                if (string.IsNullOrWhiteSpace(report.ContentJson)) continue;

                try
                {
                    var sections = JsonSerializer.Deserialize<List<DiagnosticSection>>(report.ContentJson);
                    if (sections != null)
                    {
                        foreach (var section in sections)
                        {
                            // Gather existing findings from collectors
                            foreach (var finding in section.Findings)
                            {
                                findings.Add(finding);
                                recommendations.AddRange(finding.Recommendations);
                            }

                            // Run dynamic analyzer rules over metrics
                            AnalyzeMetrics(section, findings, recommendations);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse content for report {ReportId} of category {Category}.", report.ReportId, report.Category);
                }
            }

            // Deduplicate findings by RuleName
            var uniqueFindings = findings
                .GroupBy(f => f.RuleName)
                .Select(g => g.First() with { Recommendations = g.SelectMany(f => f.Recommendations).Distinct().ToList() })
                .ToList();

            // Calculate overall health score
            double healthScore = 100.0;
            foreach (var finding in uniqueFindings)
            {
                switch (finding.Severity.ToUpperInvariant())
                {
                    case "EMERGENCY":
                        healthScore -= 50.0;
                        break;
                    case "CRITICAL":
                        healthScore -= 25.0;
                        break;
                    case "WARNING":
                        healthScore -= 10.0;
                        break;
                    case "INFORMATION":
                        healthScore -= 1.0;
                        break;
                }
            }

            healthScore = Math.Clamp(healthScore, 0.0, 100.0);

            // Determine health tier status string
            string overallStatus = "Healthy";
            if (healthScore < 50.0 || uniqueFindings.Any(f => f.Severity.Equals("Emergency", StringComparison.OrdinalIgnoreCase)))
            {
                overallStatus = "Emergency";
            }
            else if (healthScore < 75.0 || uniqueFindings.Any(f => f.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase)))
            {
                overallStatus = "Critical";
            }
            else if (healthScore < 95.0 || uniqueFindings.Any(f => f.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase)))
            {
                overallStatus = "Warning";
            }

            // Prioritize findings: Emergency -> Critical -> Warning -> Information
            var prioritizedFindings = uniqueFindings
                .OrderBy(f => GetSeverityPriority(f.Severity))
                .ToList();

            // Deduplicate recommendations
            var consolidatedRecs = recommendations
                .GroupBy(r => r.Description)
                .Select(g => g.First())
                .OrderBy(r => GetRecommendationPriority(r.Priority))
                .ToList();

            _logger.LogInformation("Analysis complete for machine {MachineId}. HealthScore: {Score}, Status: {Status}, Findings: {FindingsCount}",
                machineId, healthScore, overallStatus, prioritizedFindings.Count);

            return new DiagnosticResult
            {
                DiagnosticId = diagnosticId,
                MachineId = machineId,
                HealthScore = healthScore,
                OverallStatus = overallStatus,
                Reports = reports,
                Findings = prioritizedFindings,
                Recommendations = consolidatedRecs,
                IsSuccess = true,
                StartedAtUtc = DateTime.UtcNow,
                EndedAtUtc = DateTime.UtcNow
            };
        }

        private void AnalyzeMetrics(DiagnosticSection section, List<DiagnosticFinding> findings, List<DiagnosticRecommendation> recommendations)
        {
            foreach (var metric in section.Metrics)
            {
                if (double.TryParse(metric.Value, out double val))
                {
                    if (metric.Name == "CpuUsage" && val > 85)
                    {
                        var rec = new DiagnosticRecommendation
                        {
                            Description = "High CPU resource consumption detected.",
                            ActionableStep = "Identify high CPU processes using Process Supervisor and terminate if non-critical.",
                            Priority = "High"
                        };
                        findings.Add(new DiagnosticFinding
                        {
                            RuleName = "CpuSaturated",
                            Severity = val > 95 ? "Emergency" : "Critical",
                            Description = $"CPU utilization is extremely high: {val:F2}%.",
                            Category = "Performance",
                            Recommendations = new List<DiagnosticRecommendation> { rec }
                        });
                        recommendations.Add(rec);
                    }
                    else if (metric.Name == "AvailableRam" && val < 2.0)
                    {
                        var rec = new DiagnosticRecommendation
                        {
                            Description = "Workstation is low on available physical memory (RAM).",
                            ActionableStep = "Trigger memory cache flushing or close idle gaming clients.",
                            Priority = "Medium"
                        };
                        findings.Add(new DiagnosticFinding
                        {
                            RuleName = "LowMemoryAvailable",
                            Severity = val < 1.0 ? "Critical" : "Warning",
                            Description = $"Available system physical memory is low: {val:F2} GB remaining.",
                            Category = "Performance",
                            Recommendations = new List<DiagnosticRecommendation> { rec }
                        });
                        recommendations.Add(rec);
                    }
                    else if (metric.Name == "PrimaryInstallationDriveFreeSpace" && val < 10.0)
                    {
                        var rec = new DiagnosticRecommendation
                        {
                            Description = "Primary system installation partition is running out of disk space.",
                            ActionableStep = "Execute system cleanup, purge old diagnostics packages, and clean temporary downloads.",
                            Priority = "High"
                        };
                        findings.Add(new DiagnosticFinding
                        {
                            RuleName = "LowDiskSpace",
                            Severity = val < 5.0 ? "Critical" : "Warning",
                            Description = $"Primary installation disk space is extremely low: {val:F2} GB free.",
                            Category = "Storage",
                            Recommendations = new List<DiagnosticRecommendation> { rec }
                        });
                        recommendations.Add(rec);
                    }
                    else if (metric.Name == "PingLatencyMs" && val > 100.0)
                    {
                        var rec = new DiagnosticRecommendation
                        {
                            Description = "High network latency to central gateway or internet CDN.",
                            ActionableStep = "Verify network interfaces connections, adapter configurations, and run latency probes.",
                            Priority = "Medium"
                        };
                        findings.Add(new DiagnosticFinding
                        {
                            RuleName = "HighNetworkLatency",
                            Severity = val > 200.0 ? "Critical" : "Warning",
                            Description = $"Ping latency is higher than SLA thresholds: {val:F2} ms.",
                            Category = "Network",
                            Recommendations = new List<DiagnosticRecommendation> { rec }
                        });
                        recommendations.Add(rec);
                    }
                }
            }
        }

        private int GetSeverityPriority(string severity)
        {
            return severity.ToUpperInvariant() switch
            {
                "EMERGENCY" => 1,
                "CRITICAL" => 2,
                "WARNING" => 3,
                "INFORMATION" => 4,
                _ => 5
            };
        }

        private int GetRecommendationPriority(string priority)
        {
            return priority.ToUpperInvariant() switch
            {
                "HIGH" => 1,
                "MEDIUM" => 2,
                "LOW" => 3,
                _ => 4
            };
        }
    }
}
