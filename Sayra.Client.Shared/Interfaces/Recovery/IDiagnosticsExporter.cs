using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Recovery;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Contract for an extensible diagnostics exporter (e.g., JSON, Plain Text, HTML, PDF).
    /// </summary>
    public interface IDiagnosticsExporter
    {
        /// <summary>
        /// Gets the format name that this exporter supports (case-insensitive).
        /// </summary>
        string Format { get; }

        /// <summary>
        /// Exports the content asynchronously to the destination path.
        /// </summary>
        /// <param name="reportType">The type of report.</param>
        /// <param name="content">The report content or data payload.</param>
        /// <param name="destinationPath">The target file path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task returning the final destination path.</returns>
        Task<string> ExportAsync(ReportType reportType, string content, string destinationPath, CancellationToken cancellationToken = default);
    }
}
