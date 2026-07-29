using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Recovery;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Contract for collecting, analyzing, and persisting workstation diagnostic and health reports.
    /// </summary>
    public interface IRecoveryDiagnosticsEngine
    {
        /// <summary>
        /// Generates and persists all standard diagnostics reports (Startup, Health, Recovery, Failure, Resource, Security) locally.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task GenerateAndPersistAllReportsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a report focused on workstation startup checks and previous shutdown status.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The generated startup report content.</returns>
        Task<string> GenerateStartupReportAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a comprehensive periodic health status summary across all registered subsystems.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The generated health report content.</returns>
        Task<string> GenerateHealthReportAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a comprehensive periodic health status summary across all registered subsystems.
        /// Retained for backward compatibility.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The generated health summary report content.</returns>
        Task<string> GenerateHealthSummaryReportAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a report compiling all self-healing events, success rates, and recovery attempts.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The generated recovery report content.</returns>
        Task<string> GenerateRecoveryReportAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a specialized report focusing on currently active failure states or recent exceptions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The generated failure report content.</returns>
        Task<string> GenerateFailureReportAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a resource report detailing CPU, Memory, Disk, Network, GPU, thread and handle counts, and pressure levels.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The generated resource report content.</returns>
        Task<string> GenerateResourceReportAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a security report summarizing cryptographic validations of database, config, policy, media, plugins, packages, and executables.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The generated security report content.</returns>
        Task<string> GenerateSecurityReportAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a complete aggregated diagnostics payload containing all individual reports.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The aggregated full diagnostics payload.</returns>
        Task<string> GenerateFullDiagnosticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports a specific diagnostics report type in the requested format (e.g., "JSON", "TXT") to the target path.
        /// </summary>
        /// <param name="reportType">The target report type.</param>
        /// <param name="format">The target file format (JSON, TXT, etc.).</param>
        /// <param name="destinationPath">Optional custom destination path. If not provided, a default path inside the configured directory is used.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The path of the exported file.</returns>
        Task<string> ExportDiagnosticsAsync(ReportType reportType, string format, string? destinationPath = null, CancellationToken cancellationToken = default);
    }
}
