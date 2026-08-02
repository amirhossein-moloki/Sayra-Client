using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Abstracts pluggable historical telemetry archive and backup operations.
    /// </summary>
    public interface IHistoricalArchiveProvider
    {
        /// <summary>
        /// Gets the identifying name of the active archive provider.
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Creates a secure archive file containing the specified historical metrics.
        /// </summary>
        /// <param name="archiveFilePath">Destination file path of the archive.</param>
        /// <param name="metrics">The collection of metrics to archive.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task ArchiveAsync(string archiveFilePath, IReadOnlyCollection<HistoricalMetric> metrics, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores a collection of historical metrics from an archive file.
        /// </summary>
        /// <param name="archiveFilePath">Source path of the archive file.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The restored historical metrics collection.</returns>
        Task<IReadOnlyCollection<HistoricalMetric>> RestoreAsync(string archiveFilePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the structure, integrity, and authenticity of an archive file.
        /// </summary>
        /// <param name="archiveFilePath">Path of the archive file to validate.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>True if the archive is valid; otherwise, false.</returns>
        Task<bool> ValidateArchiveAsync(string archiveFilePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts the metadata catalog associated with the specified archive file.
        /// </summary>
        /// <param name="archiveFilePath">Path of the archive file.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A dictionary containing metadata descriptors (e.g. version, count, machine, timestamps).</returns>
        Task<Dictionary<string, string>> GetArchiveMetadataAsync(string archiveFilePath, CancellationToken cancellationToken = default);
    }
}
