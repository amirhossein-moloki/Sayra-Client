using System;

namespace Sayra.Client.Diagnostics.Configuration
{
    public class DiagnosticsOptions
    {
        public const string SectionName = "Diagnostics";

        public int PollingIntervalMs { get; set; } = 1000;
        public int CacheDurationMinutes { get; set; } = 5;
        public bool EnableGpuMonitoring { get; set; } = true;
        public bool EnableNetworkMonitoring { get; set; } = true;
        public bool EnableStorageMonitoring { get; set; } = true;
        public bool EnableTemperatureMonitoring { get; set; } = true;
        public bool LogHardwareChanges { get; set; } = true;
        public int ValidationRetryCount { get; set; } = 3;
    }
}
