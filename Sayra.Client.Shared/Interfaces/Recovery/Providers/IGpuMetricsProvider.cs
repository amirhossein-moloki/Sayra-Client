using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Recovery.Providers
{
    /// <summary>
    /// Abstraction for gathering system GPU and hardware temperature metrics.
    /// </summary>
    public interface IGpuMetricsProvider
    {
        /// <summary>
        /// Retrieves the overall GPU usage percentage (0.0 to 100.0).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>GPU usage percentage.</returns>
        Task<double> GetGpuUsagePercentageAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the overall hardware or GPU temperature in degrees Celsius, if available.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Temperature in degrees Celsius, or null if unavailable.</returns>
        Task<double?> GetHardwareTemperatureCelsiusAsync(CancellationToken cancellationToken = default);
    }
}
