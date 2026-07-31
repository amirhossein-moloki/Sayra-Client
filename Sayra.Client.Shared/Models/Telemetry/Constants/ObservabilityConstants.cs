using System;

namespace Sayra.Client.Shared.Models.Telemetry.Constants
{
    /// <summary>
    /// Centralized registry of all constants, default threshold limits, configurations, and metric identifiers used across the Observability platform.
    /// </summary>
    public static class ObservabilityConstants
    {
        /// <summary>
        /// Centralized default collection intervals as TimeSpans matching Phase 8 Section 6 specifications.
        /// </summary>
        public static class DefaultIntervals
        {
            /// <summary>Critical process and supervisor watchdog collection interval (5 seconds).</summary>
            public static readonly TimeSpan Critical = TimeSpan.FromSeconds(5);

            /// <summary>Application and transport latencies performance monitoring interval (15 seconds).</summary>
            public static readonly TimeSpan Performance = TimeSpan.FromSeconds(15);

            /// <summary>Hardware utilization sensor metrics collection interval (30 seconds).</summary>
            public static readonly TimeSpan Hardware = TimeSpan.FromSeconds(30);

            /// <summary>Disk storage and system capacity collection interval (60 seconds).</summary>
            public static readonly TimeSpan Storage = TimeSpan.FromSeconds(60);

            /// <summary>Long-term consolidated metrics downsampling interval (5 minutes).</summary>
            public static readonly TimeSpan Historical = TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Enterprise metric naming keys preventing magic strings and duplicate identifiers.
        /// </summary>
        public static class MetricNames
        {
            /// <summary>CPU utilization percentage.</summary>
            public const string CpuUsage = "system.cpu.usage";

            /// <summary>Memory usage percentage.</summary>
            public const string MemoryUsage = "system.memory.usage";

            /// <summary>GPU utilization percentage.</summary>
            public const string GpuUsage = "system.gpu.usage";

            /// <summary>Disk I/O latency duration.</summary>
            public const string DiskLatency = "system.disk.latency";

            /// <summary>Database query response latency.</summary>
            public const string DatabaseLatency = "app.database.latency";

            /// <summary>Local Named Pipe IPC latency.</summary>
            public const string IpcLatency = "app.ipc.latency";

            /// <summary>Secure SslStream TCP transport latency.</summary>
            public const string TcpLatency = "app.tcp.latency";

            /// <summary>Update package file download speed in bytes per second.</summary>
            public const string DownloadSpeed = "app.download.speed";

            /// <summary>Payload upload transfer speed in bytes per second.</summary>
            public const string UploadSpeed = "app.upload.speed";

            /// <summary>Workstation application cache hit ratio.</summary>
            public const string CacheHitRatio = "app.cache.hit_ratio";

            /// <summary>Workstation persistent offline transmission queue length.</summary>
            public const string QueueLength = "app.queue.length";

            /// <summary>Supervisor background worker thread execution duration.</summary>
            public const string WorkerExecutionTime = "app.worker.execution_time";
        }

        /// <summary>
        /// Monitored system and application subsystem name identifiers.
        /// </summary>
        public static class SubsystemNames
        {
            /// <summary>Authentication and session authorization module.</summary>
            public const string Authentication = "Authentication";

            /// <summary>SQLCipher persistent database module.</summary>
            public const string Database = "Database";

            /// <summary>Secure SSL network socket transport module.</summary>
            public const string Network = "Network";

            /// <summary>Named Pipe IPC bridge and client server communications.</summary>
            public const string IPC = "IPC";

            /// <summary>WPF and local background notification deliveries.</summary>
            public const string Notifications = "Notifications";

            /// <summary>Update and ad campaign media downloader.</summary>
            public const string Downloads = "Downloads";

            /// <summary>Atomic file staging and RM installation services.</summary>
            public const string Updates = "Updates";

            /// <summary>Local ad rotator and content panels.</summary>
            public const string Media = "Media";

            /// <summary>Dynamic engine extension plugins.</summary>
            public const string Plugins = "Plugins";

            /// <summary>Platform monitoring, metrics, and tracing platform.</summary>
            public const string Telemetry = "Telemetry";

            /// <summary>Resilient self-healing and crash rollback manager.</summary>
            public const string Recovery = "Recovery";

            /// <summary>Interactive desktop isolation and anti-tampering guards.</summary>
            public const string Security = "Security";

            /// <summary>Admin central policy configurations registry.</summary>
            public const string Policies = "Policies";

            /// <summary>Deadlock, queue length, and thread freezes monitor.</summary>
            public const string Watchdog = "Watchdog";

            /// <summary>DirectX overlay gameplay window renderer.</summary>
            public const string Overlay = "Overlay";

            /// <summary>Cloud server ad and configuration sync worker.</summary>
            public const string Synchronization = "Synchronization";
        }

        /// <summary>
        /// Registry keys for loading and saving persistence configuration data.
        /// </summary>
        public static class StorageKeys
        {
            /// <summary>The default table name for historical consolidated metrics in SQLCipher SQLite.</summary>
            public const string HistoricalMetricsTable = "HistoricalMetrics";

            /// <summary>The default table name for active and historical alert records in SQLCipher SQLite.</summary>
            public const string ActiveAlertsTable = "ActiveAlerts";

            /// <summary>The database encryption key binary file name.</summary>
            public const string DbKeyFileName = "db_key.bin";
        }

        /// <summary>
        /// Appsettings configuration section binding paths.
        /// </summary>
        public static class ConfigurationKeys
        {
            /// <summary>Base section path for all observability settings.</summary>
            public const string RootSection = "Observability";

            /// <summary>Section path for telemetry options.</summary>
            public const string Telemetry = "Observability:Telemetry";

            /// <summary>Section path for mathematical metrics aggregates options.</summary>
            public const string Metrics = "Observability:Metrics";

            /// <summary>Section path for distributed tracing context options.</summary>
            public const string Tracing = "Observability:Tracing";

            /// <summary>Section path for latency performance threshold options.</summary>
            public const string Performance = "Observability:Performance";

            /// <summary>Section path for diagnostics engine options.</summary>
            public const string Diagnostics = "Observability:Diagnostics";

            /// <summary>Section path for alerting threshold options.</summary>
            public const string Alerts = "Observability:Alerts";

            /// <summary>Section path for administration dashboard options.</summary>
            public const string Dashboard = "Observability:Dashboard";

            /// <summary>Section path for long-term SQLCipher database options.</summary>
            public const string HistoricalStorage = "Observability:HistoricalStorage";

            /// <summary>Section path for general platform monitoring options.</summary>
            public const string Monitoring = "Observability:Monitoring";

            /// <summary>Section path for database retention options.</summary>
            public const string Retention = "Observability:Retention";

            /// <summary>Section path for collection loops options.</summary>
            public const string Collection = "Observability:Collection";
        }

        /// <summary>
        /// Telemetry capacity limits preventing memory bloat under load.
        /// </summary>
        public static class TelemetryLimits
        {
            /// <summary>The absolute maximum number of telemetry records held in the volatile collection buffer.</summary>
            public const int MaxBufferedRecords = 10000;

            /// <summary>The absolute minimum allowed telemetry buffer size.</summary>
            public const int MinBufferedRecords = 10;
        }

        /// <summary>
        /// Default alert rule threshold levels.
        /// </summary>
        public static class AlertDefaults
        {
            /// <summary>Default warning CPU utilization trigger level (90%).</summary>
            public const double WarningCpuPercent = 90.0;

            /// <summary>Default warning memory utilization trigger level (90%).</summary>
            public const double WarningMemoryPercent = 90.0;

            /// <summary>Default warning free disk capacity level (10%).</summary>
            public const double WarningDiskFreePercent = 10.0;
        }

        /// <summary>
        /// Distributed tracing default configuration parameters.
        /// </summary>
        public static class TracingDefaults
        {
            /// <summary>The default context propagation key name for IPC pipelines.</summary>
            public const string ContextHeaderKey = "X-SAYRA-Trace-Context";

            /// <summary>The default trace identifier header name.</summary>
            public const string TraceIdHeaderKey = "X-SAYRA-Trace-Id";
        }

        /// <summary>
        /// Administration panel dashboard widgets display parameters.
        /// </summary>
        public static class DashboardDefaults
        {
            /// <summary>Default maximum active alert count displayed in visual dashboard feeds.</summary>
            public const int MaxDashboardAlerts = 50;

            /// <summary>The minimum dashboard refresh poll cycle duration (1 second).</summary>
            public const int MinRefreshSeconds = 1;
        }
    }
}
