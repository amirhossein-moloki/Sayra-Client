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
    }
}
