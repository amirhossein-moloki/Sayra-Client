using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry
{
    /// <summary>
    /// Production implementation of IMetricsCollector.
    /// Captures, records, and exposes real-time workstation subsystem metrics.
    /// Supports a clean thread-safe transactional collection cycle.
    /// </summary>
    public class MetricsCollector : IMetricsCollector
    {
        private readonly ILogger<MetricsCollector> _logger;
        private readonly ITelemetryService _telemetryService;
        private readonly ConcurrentQueue<MetricPoint> _collectedMetrics = new();

        public MetricsCollector(ITelemetryService telemetryService, ILogger<MetricsCollector> logger)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task RecordMetricAsync(string name, double value, IReadOnlyDictionary<string, string>? tags = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));

            var point = new MetricPoint
            {
                Timestamp = DateTime.UtcNow,
                Value = value,
                Tags = tags ?? new Dictionary<string, string>()
            };

            // Enqueue locally for the current collection cycle retrieval
            _collectedMetrics.Enqueue(point);

            // Dynamically construct and track a TelemetryRecord for the central engine
            var record = new TelemetryRecord
            {
                Timestamp = point.Timestamp,
                MetricName = name,
                Category = DetermineCategory(name),
                Value = value,
                Unit = DetermineUnit(name),
                Source = "MetricsCollector",
                Severity = MetricSeverity.Info,
                Tags = point.Tags
            };

            await _telemetryService.TrackMetricAsync(record, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task<IReadOnlyCollection<MetricPoint>> GetCollectedMetricsAsync(CancellationToken cancellationToken = default)
        {
            var list = new List<MetricPoint>();
            while (_collectedMetrics.TryDequeue(out var point))
            {
                list.Add(point);
            }
            return Task.FromResult<IReadOnlyCollection<MetricPoint>>(list);
        }

        private MetricCategory DetermineCategory(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains(".cpu")) return MetricCategory.Cpu;
            if (lower.Contains(".memory") || lower.Contains(".ram")) return MetricCategory.Memory;
            if (lower.Contains(".gpu") || lower.Contains(".vram") || lower.Contains(".fps")) return MetricCategory.Gpu;
            if (lower.Contains(".disk") || lower.Contains(".storage")) return MetricCategory.Disk;
            if (lower.Contains(".network") || lower.Contains(".ping")) return MetricCategory.Network;
            if (lower.Contains(".database") || lower.Contains(".sql")) return MetricCategory.Database;
            if (lower.Contains(".ipc") || lower.Contains(".pipe")) return MetricCategory.Ipc;
            if (lower.Contains(".sync")) return MetricCategory.Sync;
            if (lower.Contains(".notification")) return MetricCategory.Notification;
            if (lower.Contains(".overlay")) return MetricCategory.Overlay;
            if (lower.Contains(".watchdog")) return MetricCategory.Watchdog;
            if (lower.Contains(".policy")) return MetricCategory.Policy;
            if (lower.Contains(".plugin")) return MetricCategory.Plugin;
            if (lower.Contains(".download")) return MetricCategory.Download;
            if (lower.Contains(".update")) return MetricCategory.Update;
            if (lower.Contains(".game")) return MetricCategory.Game;
            if (lower.Contains(".session")) return MetricCategory.Session;

            return MetricCategory.Process;
        }

        private MetricUnit DetermineUnit(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains(".usage") || lower.Contains(".percent")) return MetricUnit.Percent;
            if (lower.Contains(".ms") || lower.Contains(".latency") || lower.Contains(".ping")) return MetricUnit.Milliseconds;
            if (lower.Contains(".duration") || lower.Contains(".seconds")) return MetricUnit.Seconds;
            if (lower.Contains(".bytes_") || lower.Contains(".speed")) return MetricUnit.Bytes;
            if (lower.Contains(".mb") || lower.Contains(".vram")) return MetricUnit.Megabytes;
            if (lower.Contains(".gb") || lower.Contains(".space")) return MetricUnit.Gigabytes;
            if (lower.Contains(".rate") || lower.Contains(".fps")) return MetricUnit.Rate;

            return MetricUnit.Count;
        }
    }
}
