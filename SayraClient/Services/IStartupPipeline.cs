using System.Threading;
using System.Threading.Tasks;

namespace SayraClient.Services;

/// <summary>
/// Manages the structured, multi-stage startup pipeline for the enterprise client.
/// </summary>
public interface IStartupPipeline
{
    /// <summary>
    /// Executes the complete 10-stage startup pipeline.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Task representing the asynchronous startup operation.</returns>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
