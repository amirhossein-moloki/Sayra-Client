using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Telemetry.Diagnostics
{
    /// <summary>
    /// Represents an independently executable diagnostic module that evaluates workstation status.
    /// </summary>
    public interface IDiagnosticModule
    {
        /// <summary>
        /// Gets the unique identifying name of the diagnostic module.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the name of the subsystem affected by this module.
        /// </summary>
        string AffectedSubsystem { get; }

        /// <summary>
        /// Asynchronously executes diagnostics check on the specific subsystem.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop execution.</param>
        /// <returns>A module result detailing health status and recommendations.</returns>
        Task<DiagnosticModuleResult> ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
