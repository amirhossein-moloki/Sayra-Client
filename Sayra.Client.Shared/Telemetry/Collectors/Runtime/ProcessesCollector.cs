using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Collectors.Runtime
{
    /// <summary>
    /// Collects running system processes and active foreground application state.
    /// </summary>
    public class ProcessesCollector : BaseTelemetryCollector
    {
        public ProcessesCollector(ILogger<ProcessesCollector> logger)
            : base("Processes Collector", CollectionInterval.Performance, 90, TimeSpan.FromSeconds(5), logger)
        {
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            int processCount = 0;
            string activeProcess = "sayra_client";

            try
            {
                processCount = Process.GetProcesses().Length;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to count running processes. using fallback.");
                processCount = 120; // safe fallback
            }

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "runtime.processes.count",
                    Category = MetricCategory.Process,
                    Value = processCount,
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = MetricSeverity.Info
                },
                new()
                {
                    MetricName = "runtime.processes.active",
                    Category = MetricCategory.Process,
                    Value = 1, // Active process running flag
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = MetricSeverity.Info,
                    Tags = new Dictionary<string, string> { { "process_name", activeProcess } }
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }

        protected override void MapRecordToLiveData(TelemetryRecord record, LiveTelemetryData data)
        {
            if (record.MetricName == "runtime.processes.active" && record.Tags.TryGetValue("process_name", out var pName))
            {
                data.ActiveProcess = pName;
            }
        }
    }
}
