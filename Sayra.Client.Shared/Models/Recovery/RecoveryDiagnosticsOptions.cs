using System;
using System.IO;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Configurable options for the Enterprise Recovery Diagnostics Engine.
    /// </summary>
    public class RecoveryDiagnosticsOptions
    {
        /// <summary>
        /// Configuration section name.
        /// </summary>
        public const string SectionName = "Recovery:Diagnostics";

        /// <summary>
        /// Gets or sets the local directory where generated reports are persisted.
        /// </summary>
        public string ReportsDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "diagnostics_reports");

        /// <summary>
        /// Gets or sets the retention count for files.
        /// When the count of files exceeds this limit, the oldest files are automatically pruned.
        /// </summary>
        public int RetentionLimit { get; set; } = 50;

        /// <summary>
        /// Gets or sets whether local JSON report serialization is enabled.
        /// </summary>
        public bool EnableJson { get; set; } = true;

        /// <summary>
        /// Gets or sets whether local plain text report serialization is enabled.
        /// </summary>
        public bool EnableText { get; set; } = true;

        /// <summary>
        /// Gets or sets the application/client version displayed in diagnostics metadata.
        /// </summary>
        public string ApplicationVersion { get; set; } = "1.0.0.0";

        /// <summary>
        /// Gets or sets the build number displayed in diagnostics metadata.
        /// </summary>
        public string BuildNumber { get; set; } = "Release.2025.2";

        /// <summary>
        /// Gets or sets the identity of the generator.
        /// </summary>
        public string GeneratedBy { get; set; } = "SAYRA Recovery Diagnostics Engine";

        /// <summary>
        /// Gets or sets the version of the diagnostics schema.
        /// </summary>
        public string ReportVersion { get; set; } = "1.0";
    }
}
