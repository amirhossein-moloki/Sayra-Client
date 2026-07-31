using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    /// <summary>
    /// Legacy system diagnostics report engine.
    /// </summary>
    public interface IDiagnosticsEngine
    {
        /// <summary>
        /// Generates a legacy full diagnostics report.
        /// </summary>
        Task<SystemDiagnosticsReport> GenerateFullReportAsync(CancellationToken cancellationToken = default);
    }
}
