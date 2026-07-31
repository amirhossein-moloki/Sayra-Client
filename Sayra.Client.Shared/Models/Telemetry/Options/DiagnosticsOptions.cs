using System.ComponentModel.DataAnnotations;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing workstation diagnostics engine captures.
    /// </summary>
    public class DiagnosticsOptions
    {
        /// <summary>
        /// Gets or sets the execution frequency of thread dumps and stack trace logs in seconds.
        /// </summary>
        [Range(10, 86400, ErrorMessage = "ThreadDumpIntervalSeconds must be between 10 and 86400.")]
        public int ThreadDumpIntervalSeconds { get; set; } = 300;

        /// <summary>
        /// Gets or sets the size limit for capturing localized memory snapshots in megabytes.
        /// </summary>
        [Range(10, 4096, ErrorMessage = "MemorySnapshotLimitMegabytes must be between 10 and 4096.")]
        public int MemorySnapshotLimitMegabytes { get; set; } = 1024;
    }
}
