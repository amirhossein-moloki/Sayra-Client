using System;
using System.Collections.Generic;

namespace Sayra.Client.Diagnostics.Models
{
    public record HardwareMetrics(
        double CpuUsage,
        double GpuUsage,
        double RamUsage,
        double VramUsage,
        double DiskUsage,
        double NetworkUsage,
        TimeSpan Uptime,
        List<string> ActiveProcesses,
        double RefreshRate,
        double FpsCapability
    );
}
