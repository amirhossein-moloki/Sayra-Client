using System;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Represents comprehensive system resource usage metrics on the workstation.
    /// This model is immutable and serializable.
    /// </summary>
    public class ResourceMetrics
    {
        /// <summary>
        /// Gets the timestamp when these metrics were queried.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets the total system CPU usage percentage (0.0 to 100.0).
        /// </summary>
        public double CpuUsagePercentage { get; init; }

        /// <summary>
        /// Gets the RAM working set bytes utilized by the host process.
        /// </summary>
        public long ProcessRamBytes { get; init; }

        /// <summary>
        /// Gets the total system physical memory usage bytes.
        /// </summary>
        public long TotalSystemRamBytes { get; init; }

        /// <summary>
        /// Gets the available physical memory bytes.
        /// </summary>
        public long AvailableSystemRamBytes { get; init; }

        /// <summary>
        /// Gets the available free storage space bytes on the primary installation drive.
        /// </summary>
        public long FreeDiskSpaceBytes { get; init; }

        /// <summary>
        /// Gets the number of operating system handles currently opened by the process.
        /// </summary>
        public int HandleCount { get; init; }

        /// <summary>
        /// Gets the number of threads currently active in the process.
        /// </summary>
        public int ThreadCount { get; init; }

        /// <summary>
        /// Gets the number of allocated GDI objects (Windows GUI resource handle count).
        /// </summary>
        public int GdiObjectsCount { get; init; }

        /// <summary>
        /// Gets the GPU usage percentage (0.0 to 100.0), if supported.
        /// </summary>
        public double GpuUsagePercentage { get; init; }

        /// <summary>
        /// Gets the Disk Input/Output operations per second (or bytes read/written per second).
        /// </summary>
        public double DiskIoBytesPerSecond { get; init; }

        /// <summary>
        /// Gets the Network Input/Output transmission rate in bytes per second.
        /// </summary>
        public double NetworkIoBytesPerSecond { get; init; }

        /// <summary>
        /// Gets the hardware temperature in degrees Celsius, if available.
        /// </summary>
        public double? HardwareTemperatureCelsius { get; init; }

        /// <summary>
        /// Gets the evaluated resource pressure level based on the metrics.
        /// </summary>
        public ResourcePressureLevel PressureLevel { get; init; } = ResourcePressureLevel.Normal;
    }
}
