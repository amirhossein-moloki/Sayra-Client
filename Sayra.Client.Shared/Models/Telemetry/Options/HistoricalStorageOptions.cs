using System.ComponentModel.DataAnnotations;

namespace Sayra.Client.Shared.Models.Telemetry.Options
{
    /// <summary>
    /// Configuration options governing SQLCipher-encrypted SQLite historical metrics storage.
    /// </summary>
    public class HistoricalStorageOptions
    {
        /// <summary>
        /// Gets or sets the target database file location.
        /// </summary>
        [Required(AllowEmptyStrings = false, ErrorMessage = "DatabasePath cannot be null or empty.")]
        public string DatabasePath { get; set; } = "Data/historical_metrics.db";

        /// <summary>
        /// Gets or sets a value indicating whether standard database encryption compression is enabled.
        /// </summary>
        public bool UseCompression { get; set; } = true;

        /// <summary>
        /// Gets or sets the SQLCipher storage page block allocation size in bytes.
        /// </summary>
        [Range(512, 65536, ErrorMessage = "PageSize must be between 512 and 65536.")]
        public int PageSize { get; set; } = 4096;

        /// <summary>
        /// Gets or sets the batch size for database writes.
        /// </summary>
        [Range(1, 10000, ErrorMessage = "BatchSize must be between 1 and 10000.")]
        public int BatchSize { get; set; } = 100;

        /// <summary>
        /// Gets or sets the maximum historical storage size in bytes.
        /// </summary>
        [Range(1024, 107374182400, ErrorMessage = "MaxStorageSizeBytes must be between 1024 and 100 GB.")]
        public long MaxStorageSizeBytes { get; set; } = 104857600; // Default 100 MB

        /// <summary>
        /// Gets or sets the directory where exported archives are persisted.
        /// </summary>
        [Required(AllowEmptyStrings = false, ErrorMessage = "ArchiveDirectory cannot be null or empty.")]
        public string ArchiveDirectory { get; set; } = "Data/Archive";

        /// <summary>
        /// Gets or sets a custom retention window in hours. Takes precedence over RetentionOptions.RetentionDays if configured.
        /// </summary>
        [Range(1, 87600, ErrorMessage = "CustomRetentionHours must be between 1 and 10 years.")]
        public int? CustomRetentionHours { get; set; }
    }
}
