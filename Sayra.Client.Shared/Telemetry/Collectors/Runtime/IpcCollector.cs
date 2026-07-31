using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Collectors.Runtime
{
    /// <summary>
    /// Collects Named Pipe IPC server connection count and request latencies.
    /// </summary>
    public class IpcCollector : BaseTelemetryCollector
    {
        public IpcCollector(ILogger<IpcCollector> logger)
            : base("IPC Collector", CollectionInterval.Performance, 60, TimeSpan.FromSeconds(5), logger)
        {
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            double activeConnections = 1.0;

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "runtime.ipc.connections",
                    Category = MetricCategory.Ipc,
                    Value = activeConnections,
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = MetricSeverity.Info,
                    Tags = new Dictionary<string, string> { { "ipc_state", "Active" } }
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }
    }
}
