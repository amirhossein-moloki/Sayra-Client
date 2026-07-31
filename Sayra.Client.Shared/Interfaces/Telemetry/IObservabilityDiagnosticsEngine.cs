using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Service responsible for compiling full workstation runtime diagnostics reports.
    /// </summary>
    public interface IDiagnosticsEngine
    {
        /// <summary>
        /// Asynchronously generates a comprehensive diagnostic report of the workstation's status.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A comprehensive diagnostic report model.</returns>
        Task<DiagnosticReport> GenerateDiagnosticsReportAsync(CancellationToken cancellationToken = default);
    }
}
