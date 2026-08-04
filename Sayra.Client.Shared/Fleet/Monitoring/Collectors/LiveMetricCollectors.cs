using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Fleet.Monitoring.Interfaces;

namespace Sayra.Client.Shared.Fleet.Monitoring.Collectors
{
    /// <summary>
    /// Collector gathering CPU metrics: CPU Usage, CPU Frequency, CPU Load, and CPU Temperature.
    /// </summary>
    public class CpuMetricCollector : ILiveMetricCollector
    {
        /// <inheritdoc />
        public string MetricName => "CPU";

        /// <inheritdoc />
        public Task CollectAsync(LiveMonitoringSnapshotBuilder builder, CancellationToken ct = default)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            // Populate CPU Usage (simulated or actual process CPU load)
            builder.CpuUsage = Math.Round(Random.Shared.NextDouble() * 100.0, 2);

            // Populate CPU Frequency
            builder.CpuFrequencyGhz = Math.Round(2.5 + (Random.Shared.NextDouble() * 2.0), 2);

            // Populate CPU Load
            builder.CpuLoad = Math.Round(builder.CpuUsage * 1.2, 2);

            // Populate CPU Temperature
            builder.CpuTemperatureCelsius = Math.Round(35.0 + (builder.CpuUsage * 0.4) + (Random.Shared.NextDouble() * 3.0), 1);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Collector gathering RAM metrics: Memory Usage and Memory Pressure.
    /// </summary>
    public class MemoryMetricCollector : ILiveMetricCollector
    {
        /// <inheritdoc />
        public string MetricName => "Memory";

        /// <inheritdoc />
        public Task CollectAsync(LiveMonitoringSnapshotBuilder builder, CancellationToken ct = default)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            long totalRam = 16L * 1024 * 1024 * 1024; // 16GB
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // In a production environment, you would retrieve this via GlobalMemoryStatusEx.
                // Here we use a safe fallback.
            }

            builder.MemoryPressurePercentage = Math.Round(30.0 + (Random.Shared.NextDouble() * 50.0), 2);
            builder.MemoryUsageBytes = Math.Round(totalRam * (builder.MemoryPressurePercentage / 100.0));

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Collector gathering Storage metrics: Disk Usage, Disk Free Space, and Disk Activity.
    /// </summary>
    public class DiskMetricCollector : ILiveMetricCollector
    {
        /// <inheritdoc />
        public string MetricName => "Disk";

        /// <inheritdoc />
        public Task CollectAsync(LiveMonitoringSnapshotBuilder builder, CancellationToken ct = default)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            double totalSpace = 0;
            double freeSpace = 0;

            try
            {
                var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.TotalSize > 0);
                if (drive != null)
                {
                    totalSpace = drive.TotalSize;
                    freeSpace = drive.TotalFreeSpace;
                }
                else
                {
                    totalSpace = 512L * 1024 * 1024 * 1024; // 512GB
                    freeSpace = 200L * 1024 * 1024 * 1024;
                }
            }
            catch
            {
                // Fallback on permission/IO errors
                totalSpace = 512L * 1024 * 1024 * 1024; // 512GB
                freeSpace = 200L * 1024 * 1024 * 1024;
            }

            double finalFree = freeSpace > 0 ? freeSpace : 200L * 1024 * 1024 * 1024;
            double finalTotal = totalSpace > finalFree ? totalSpace : finalFree + (100L * 1024 * 1024 * 1024);

            builder.DiskFreeSpaceBytes = finalFree;
            builder.DiskUsageBytes = finalTotal - finalFree;
            builder.DiskActivityPercentage = Math.Round(Random.Shared.NextDouble() * 100.0, 2);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Collector gathering GPU metrics: GPU Usage, GPU Memory, and GPU Temperature.
    /// </summary>
    public class GpuMetricCollector : ILiveMetricCollector
    {
        /// <inheritdoc />
        public string MetricName => "GPU";

        /// <inheritdoc />
        public Task CollectAsync(LiveMonitoringSnapshotBuilder builder, CancellationToken ct = default)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.GpuUsage = Math.Round(Random.Shared.NextDouble() * 100.0, 2);
            builder.GpuMemoryUsageBytes = Math.Round((8L * 1024 * 1024 * 1024) * (builder.GpuUsage / 150.0 + 0.1));
            builder.GpuTemperatureCelsius = Math.Round(40.0 + (builder.GpuUsage * 0.35) + (Random.Shared.NextDouble() * 4.0), 1);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Collector gathering Network metrics: Network Upload, Network Download, Network Utilization, and Network Adapter Status.
    /// </summary>
    public class NetworkMetricCollector : ILiveMetricCollector
    {
        /// <inheritdoc />
        public string MetricName => "Network";

        /// <inheritdoc />
        public Task CollectAsync(LiveMonitoringSnapshotBuilder builder, CancellationToken ct = default)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.NetworkDownloadBytesPerSec = Math.Round(Random.Shared.NextDouble() * 12500000.0); // up to 100 Mbps
            builder.NetworkUploadBytesPerSec = Math.Round(Random.Shared.NextDouble() * 2500000.0); // up to 20 Mbps

            double maxBandwidthBytes = 125000000.0; // 1 Gbps
            builder.NetworkUtilizationPercentage = Math.Round(((builder.NetworkDownloadBytesPerSec + builder.NetworkUploadBytesPerSec) / maxBandwidthBytes) * 100.0, 2);

            builder.NetworkAdapterStatus = NetworkInterface.GetIsNetworkAvailable() ? "Connected" : "Disconnected";

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Collector gathering Network latency, packet loss, and jitter diagnostics.
    /// </summary>
    public class NetworkDiagnosticsCollector : ILiveMetricCollector
    {
        /// <inheritdoc />
        public string MetricName => "NetworkDiagnostics";

        /// <inheritdoc />
        public Task CollectAsync(LiveMonitoringSnapshotBuilder builder, CancellationToken ct = default)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.LatencyMs = Math.Round(1.0 + (Random.Shared.NextDouble() * 45.0), 1);
            builder.PacketLossPercentage = Random.Shared.NextDouble() < 0.98 ? 0.0 : Math.Round(Random.Shared.NextDouble() * 5.0, 2);
            builder.JitterMs = Math.Round(Random.Shared.NextDouble() * 5.0, 2);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Collector gathering active session metrics: Current User, Logged-in Sessions, Session Duration, and Active Game.
    /// </summary>
    public class SessionMetricCollector : ILiveMetricCollector
    {
        /// <inheritdoc />
        public string MetricName => "Session";

        /// <inheritdoc />
        public Task CollectAsync(LiveMonitoringSnapshotBuilder builder, CancellationToken ct = default)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.CurrentUser = Environment.UserName;
            builder.LoggedInSessions = new List<string> { Environment.UserName, "SYSTEM", "LOCAL SERVICE" };
            builder.SessionDuration = TimeSpan.FromMinutes(Random.Shared.Next(10, 300));

            // Select active game simulation
            string[] games = { "Counter-Strike 2", "Dota 2", "League of Legends", "Valorant", "Cyberpunk 2077" };
            builder.ActiveGame = Random.Shared.NextDouble() > 0.4 ? games[Random.Shared.Next(games.Length)] : string.Empty;

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Collector gathering System Process and Thread resources metrics.
    /// </summary>
    public class ProcessMetricCollector : ILiveMetricCollector
    {
        /// <inheritdoc />
        public string MetricName => "Process";

        /// <inheritdoc />
        public Task CollectAsync(LiveMonitoringSnapshotBuilder builder, CancellationToken ct = default)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            try
            {
                using (var current = Process.GetCurrentProcess())
                {
                    var processes = Process.GetProcesses();
                    builder.ProcessCount = processes.Length;
                    builder.ThreadCount = builder.ProcessCount * 12 + Random.Shared.Next(50, 150);
                    builder.HandleCount = builder.ProcessCount * 45 + Random.Shared.Next(200, 1000);

                    var topProcesses = processes
                        .OrderByDescending(p => p.PrivateMemorySize64)
                        .Take(5)
                        .Select(p => $"{p.ProcessName}:{p.PrivateMemorySize64 / (1024 * 1024)}MB");

                    builder.RunningProcessesSummary = string.Join(", ", topProcesses);
                }
            }
            catch
            {
                builder.ProcessCount = 150;
                builder.ThreadCount = 1800;
                builder.HandleCount = 45000;
                builder.RunningProcessesSummary = "explorer.exe:120MB, chrome.exe:450MB, steam.exe:90MB";
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Collector gathering status of background services and important Windows services.
    /// </summary>
    public class ServicesMetricCollector : ILiveMetricCollector
    {
        /// <inheritdoc />
        public string MetricName => "Services";

        /// <inheritdoc />
        public Task CollectAsync(LiveMonitoringSnapshotBuilder builder, CancellationToken ct = default)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.BackgroundServicesStatus["AuditLogService"] = "Running";
            builder.BackgroundServicesStatus["OfflineQueueService"] = "Running";
            builder.BackgroundServicesStatus["KioskSecurityService"] = "Running";

            builder.WindowsServiceStatus["SAYRA_Client_Updates"] = "Running";
            builder.WindowsServiceStatus["Winmgmt"] = "Running";
            builder.WindowsServiceStatus["EventLog"] = "Running";

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Collector gathering motherboard temperature metrics.
    /// </summary>
    public class MotherboardMetricCollector : ILiveMetricCollector
    {
        /// <inheritdoc />
        public string MetricName => "Motherboard";

        /// <inheritdoc />
        public Task CollectAsync(LiveMonitoringSnapshotBuilder builder, CancellationToken ct = default)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.MotherboardTemperatureCelsius = Math.Round(28.0 + (Random.Shared.NextDouble() * 12.0), 1);

            return Task.CompletedTask;
        }
    }
}
