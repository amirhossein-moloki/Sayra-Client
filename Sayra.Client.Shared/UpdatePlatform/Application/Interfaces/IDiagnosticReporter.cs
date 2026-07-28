using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Contract responsible for aggregating environmental, system, and historical update metrics into JSON formats.
    /// </summary>
    public interface IDiagnosticReporter
    {
        /// <summary>
        /// Generates an exhaustive system diagnostic report as a serialized JSON block.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A JSON string detailing versions, errors, system info, and component states.</returns>
        Task<string> GenerateDiagnosticReportAsync(CancellationToken cancellationToken = default);
    }
}
