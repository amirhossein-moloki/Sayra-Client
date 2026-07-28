using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Coordinates with the Windows Service Control Manager (SCM) to manage elevated client background services.
    /// </summary>
    public interface IWindowsServiceManager
    {
        /// <summary>
        /// Queries the current state of a specified Windows service.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The current service state.</returns>
        Task<WindowsServiceState> GetServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the specified Windows service with custom timeout handling.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="timeout">The maximum time to wait for the operation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task StartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the specified Windows service with custom timeout handling.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="timeout">The maximum time to wait for the operation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task StopServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restarts the specified Windows service with custom timeout handling.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="timeout">The maximum time to wait for the operation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task RestartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken = default);
    }
}
