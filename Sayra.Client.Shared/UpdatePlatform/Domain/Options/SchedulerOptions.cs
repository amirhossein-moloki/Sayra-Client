using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Options
{
    /// <summary>
    /// Configuration options for the Update Scheduler.
    /// </summary>
    public class SchedulerOptions
    {
        public bool Enabled { get; set; } = true;
        public int CheckIntervalMinutes { get; set; } = 180;
        public int DownloadIntervalMinutes { get; set; } = 60;
        public int InstallIntervalMinutes { get; set; } = 120;
        public int JitterSeconds { get; set; } = 1200;
    }
}
