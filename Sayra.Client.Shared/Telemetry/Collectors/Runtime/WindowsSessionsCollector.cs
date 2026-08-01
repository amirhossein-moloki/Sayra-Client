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
    /// Collects interactive user logons and active Windows terminal session details.
    /// </summary>
    public class WindowsSessionsCollector : BaseTelemetryCollector
    {
        public WindowsSessionsCollector(ILogger<WindowsSessionsCollector> logger)
            : base("Windows Sessions Collector", CollectionInterval.Performance, 85, TimeSpan.FromSeconds(5), logger)
        {
        }

        protected override Task<IReadOnlyCollection<TelemetryRecord>> CollectInternalAsync(CancellationToken cancellationToken)
        {
            string userName = Environment.UserName;
            int sessionId = 1; // standard default for first console session

            try
            {
                // Simple diagnostic check on Windows or fallback
                sessionId = Process.GetCurrentProcess().SessionId;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to get real Windows Session Id. using fallback 1.");
            }

            var records = new List<TelemetryRecord>
            {
                new()
                {
                    MetricName = "runtime.sessions.active_id",
                    Category = MetricCategory.Session,
                    Value = sessionId,
                    Unit = MetricUnit.Count,
                    Source = Name,
                    Severity = MetricSeverity.Info,
                    Tags = new Dictionary<string, string> { { "logged_user", userName } }
                }
            };

            return Task.FromResult<IReadOnlyCollection<TelemetryRecord>>(records);
        }

        protected override void MapRecordToLiveData(TelemetryRecord record, LiveTelemetryData data)
        {
            if (record.MetricName == "runtime.sessions.active_id")
            {
                data.WindowsSessionId = (int)record.Value;
                if (record.Tags.TryGetValue("logged_user", out var user))
                {
                    data.LoggedUser = user;
                }
            }
        }
    }
}
