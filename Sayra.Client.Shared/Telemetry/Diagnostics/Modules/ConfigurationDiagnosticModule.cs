using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Telemetry.Diagnostics.Modules
{
    public class ConfigurationDiagnosticModule : IDiagnosticModule
    {
        private readonly IOptions<TelemetryOptions>? _telemetryOptions;
        private readonly IOptions<DiagnosticsOptions>? _diagnosticsOptions;
        private readonly IOptions<MetricsOptions>? _metricsOptions;

        public ConfigurationDiagnosticModule(
            IOptions<TelemetryOptions>? telemetryOptions = null,
            IOptions<DiagnosticsOptions>? diagnosticsOptions = null,
            IOptions<MetricsOptions>? metricsOptions = null)
        {
            _telemetryOptions = telemetryOptions;
            _diagnosticsOptions = diagnosticsOptions;
            _metricsOptions = metricsOptions;
        }

        public string Name => "Configuration";
        public string AffectedSubsystem => "Configuration";

        public Task<DiagnosticModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new DiagnosticModuleResult { ModuleName = Name, Status = DiagnosticHealthStatus.Healthy };

            try
            {
                bool hasErrors = false;

                if (_telemetryOptions != null)
                {
                    var opt = _telemetryOptions.Value;
                    result.Data["Telemetry_SamplingRate"] = opt.SamplingRate.ToString("F2");
                    result.Data["Telemetry_BufferSize"] = opt.BufferSize.ToString();

                    if (opt.SamplingRate < 0.0 || opt.SamplingRate > 1.0)
                    {
                        hasErrors = true;
                        result.Errors.Add("TelemetryOptions: SamplingRate is out of bounds (must be [0.0, 1.0]).");
                    }
                    if (opt.BufferSize < 10 || opt.BufferSize > 10000)
                    {
                        hasErrors = true;
                        result.Errors.Add("TelemetryOptions: BufferSize is out of bounds (must be [10, 10000]).");
                    }
                }
                else
                {
                    result.Data["TelemetryOptions"] = "Unbound";
                }

                if (_diagnosticsOptions != null)
                {
                    var opt = _diagnosticsOptions.Value;
                    result.Data["Diagnostics_ThreadDumpInterval"] = opt.ThreadDumpIntervalSeconds.ToString();
                    result.Data["Diagnostics_MemorySnapshotLimit"] = opt.MemorySnapshotLimitMegabytes.ToString();

                    if (opt.ThreadDumpIntervalSeconds < 10 || opt.ThreadDumpIntervalSeconds > 86400)
                    {
                        hasErrors = true;
                        result.Errors.Add("DiagnosticsOptions: ThreadDumpIntervalSeconds is out of bounds (must be [10, 86400]).");
                    }
                    if (opt.MemorySnapshotLimitMegabytes < 10 || opt.MemorySnapshotLimitMegabytes > 4096)
                    {
                        hasErrors = true;
                        result.Errors.Add("DiagnosticsOptions: MemorySnapshotLimitMegabytes is out of bounds (must be [10, 4096]).");
                    }
                }
                else
                {
                    result.Data["DiagnosticsOptions"] = "Unbound";
                }

                if (_metricsOptions != null)
                {
                    var opt = _metricsOptions.Value;
                    result.Data["Metrics_AggregationWindow"] = opt.AggregationWindowSeconds.ToString();

                    if (opt.AggregationWindowSeconds < 1 || opt.AggregationWindowSeconds > 3600)
                    {
                        hasErrors = true;
                        result.Errors.Add("MetricsOptions: AggregationWindowSeconds is out of bounds (must be [1, 3600]).");
                    }
                }
                else
                {
                    result.Data["MetricsOptions"] = "Unbound";
                }

                // Findings & Evaluation rules
                if (hasErrors)
                {
                    result.Status = DiagnosticHealthStatus.Degraded;
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "ConfigValidationFailed",
                        Value = "ValidationFailed",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "Configuration options violated built-in system constraints."
                    });
                }
                else
                {
                    result.Data["BindingStatus"] = "Passed";
                    result.Data["ConfigurationConsistency"] = "Consistent";
                }
            }
            catch (Exception ex)
            {
                result.Status = DiagnosticHealthStatus.Unknown;
                result.Errors.Add($"Configuration diagnostics failed: {ex.Message}");
            }

            return Task.FromResult(result);
        }
    }
}
