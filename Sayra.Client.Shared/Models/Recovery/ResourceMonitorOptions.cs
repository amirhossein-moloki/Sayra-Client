using System;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Configurable options for the Enterprise Resource Monitoring Engine.
    /// This avoids hardcoding any threshold values.
    /// </summary>
    public class ResourceMonitorOptions
    {
        /// <summary>
        /// Gets or sets the machine identifier.
        /// </summary>
        public string MachineIdentifier { get; set; } = "WS-RESOURCE-MONITOR";

        /// <summary>
        /// Gets or sets the background resource sampling interval.
        /// </summary>
        public TimeSpan SamplingInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// CPU Usage Warning threshold percentage (0.0 to 100.0).
        /// </summary>
        public double CpuWarningThreshold { get; set; } = 80.0;

        /// <summary>
        /// CPU Usage Critical threshold percentage (0.0 to 100.0).
        /// </summary>
        public double CpuCriticalThreshold { get; set; } = 90.0;

        /// <summary>
        /// CPU Usage Emergency threshold percentage (0.0 to 100.0).
        /// </summary>
        public double CpuEmergencyThreshold { get; set; } = 95.0;

        /// <summary>
        /// Process working set RAM Warning threshold in bytes.
        /// </summary>
        public long ProcessRamWarningBytes { get; set; } = 500 * 1024 * 1024; // 500 MB

        /// <summary>
        /// Process working set RAM Critical threshold in bytes.
        /// </summary>
        public long ProcessRamCriticalBytes { get; set; } = 1024 * 1024 * 1024; // 1 GB

        /// <summary>
        /// Process working set RAM Emergency threshold in bytes.
        /// </summary>
        public long ProcessRamEmergencyBytes { get; set; } = 2048 * 1024 * 1024L; // 2 GB

        /// <summary>
        /// Available system physical memory Warning threshold in bytes.
        /// </summary>
        public long SystemAvailableRamWarningBytes { get; set; } = 1024 * 1024 * 1024; // 1 GB

        /// <summary>
        /// Available system physical memory Critical threshold in bytes.
        /// </summary>
        public long SystemAvailableRamCriticalBytes { get; set; } = 512 * 1024 * 1024; // 512 MB

        /// <summary>
        /// Available system physical memory Emergency threshold in bytes.
        /// </summary>
        public long SystemAvailableRamEmergencyBytes { get; set; } = 256 * 1024 * 1024; // 256 MB

        /// <summary>
        /// Available free disk space threshold in bytes on the primary installation drive below which pressure is triggered.
        /// </summary>
        public long DiskPressureBytes { get; set; } = 500 * 1024 * 1024; // 500 MB

        /// <summary>
        /// GPU Usage Warning threshold percentage.
        /// </summary>
        public double GpuWarningThreshold { get; set; } = 80.0;

        /// <summary>
        /// GPU Usage Critical threshold percentage.
        /// </summary>
        public double GpuCriticalThreshold { get; set; } = 90.0;

        /// <summary>
        /// GPU Usage Emergency threshold percentage.
        /// </summary>
        public double GpuEmergencyThreshold { get; set; } = 95.0;

        /// <summary>
        /// Process handle count Warning threshold.
        /// </summary>
        public int HandleWarningThreshold { get; set; } = 800;

        /// <summary>
        /// Process handle count Critical threshold.
        /// </summary>
        public int HandleCriticalThreshold { get; set; } = 1000;

        /// <summary>
        /// Process handle count Emergency threshold.
        /// </summary>
        public int HandleEmergencyThreshold { get; set; } = 2000;

        /// <summary>
        /// Process active thread count Warning threshold.
        /// </summary>
        public int ThreadWarningThreshold { get; set; } = 100;

        /// <summary>
        /// Process active thread count Critical threshold.
        /// </summary>
        public int ThreadCriticalThreshold { get; set; } = 150;

        /// <summary>
        /// Process active thread count Emergency threshold.
        /// </summary>
        public int ThreadEmergencyThreshold { get; set; } = 300;

        /// <summary>
        /// Process GDI objects count Warning threshold.
        /// </summary>
        public int GdiWarningThreshold { get; set; } = 8000;

        /// <summary>
        /// Process GDI objects count Critical threshold.
        /// </summary>
        public int GdiCriticalThreshold { get; set; } = 9000;

        /// <summary>
        /// Process GDI objects count Emergency threshold.
        /// </summary>
        public int GdiEmergencyThreshold { get; set; } = 9500;

        /// <summary>
        /// Overall hardware temperature Warning threshold in degrees Celsius.
        /// </summary>
        public double TemperatureWarningThreshold { get; set; } = 85.0;

        /// <summary>
        /// Overall hardware temperature Critical threshold in degrees Celsius.
        /// </summary>
        public double TemperatureCriticalThreshold { get; set; } = 95.0;
    }
}
