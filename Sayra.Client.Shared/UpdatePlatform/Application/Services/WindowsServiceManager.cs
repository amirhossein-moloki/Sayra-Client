using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Coordinates with the Windows Service Control Manager (SCM) using System.ServiceProcess.ServiceController.
    /// Strictly handles production Windows-only service controller operations.
    /// </summary>
    public class WindowsServiceManager : IWindowsServiceManager
    {
        private readonly ILogger<WindowsServiceManager> _logger;

        public WindowsServiceManager(ILogger<WindowsServiceManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<WindowsServiceState> GetServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentException("Service name cannot be empty.", nameof(serviceName));

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await Task.Run(() =>
                {
                    using (var sc = new ServiceController(serviceName))
                    {
                        try
                        {
                            var status = sc.Status;
                            return MapServiceStatus(status);
                        }
                        catch (InvalidOperationException ex)
                        {
                            _logger.LogWarning("Service '{Service}' was not found or could not be queried.", serviceName);
                            throw new WindowsIntegrationException($"Service '{serviceName}' was not found.", ex);
                        }
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is WindowsIntegrationException || ex is OperationCanceledException))
            {
                throw new WindowsIntegrationException($"Failed to query status of service '{serviceName}'.", ex);
            }
        }

        /// <inheritdoc />
        public async Task StartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentException("Service name cannot be empty.", nameof(serviceName));

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Attempting to start service '{Service}' with timeout {Timeout}...", serviceName, timeout);

            try
            {
                await Task.Run(() =>
                {
                    using (var sc = new ServiceController(serviceName))
                    {
                        // Check if the service is disabled
                        if (sc.StartType == ServiceStartMode.Disabled)
                        {
                            throw new WindowsIntegrationException($"Service '{serviceName}' is disabled and cannot be started.");
                        }

                        if (sc.Status == ServiceControllerStatus.Running)
                        {
                            _logger.LogInformation("Service '{Service}' is already running.", serviceName);
                            return;
                        }

                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, timeout);

                        if (sc.Status != ServiceControllerStatus.Running)
                        {
                            throw new WindowsIntegrationException($"Service '{serviceName}' failed to enter Running state within timeout.");
                        }
                    }
                }, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Service '{Service}' started successfully.", serviceName);
            }
            catch (System.ServiceProcess.TimeoutException ex)
            {
                throw new WindowsIntegrationException($"Timeout exceeded while waiting for service '{serviceName}' to start.", ex);
            }
            catch (Exception ex) when (!(ex is WindowsIntegrationException || ex is OperationCanceledException))
            {
                throw new WindowsIntegrationException($"Failed to start service '{serviceName}'.", ex);
            }
        }

        /// <inheritdoc />
        public async Task StopServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentException("Service name cannot be empty.", nameof(serviceName));

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Attempting to stop service '{Service}' with timeout {Timeout}...", serviceName, timeout);

            try
            {
                await Task.Run(() =>
                {
                    using (var sc = new ServiceController(serviceName))
                    {
                        if (sc.Status == ServiceControllerStatus.Stopped)
                        {
                            _logger.LogInformation("Service '{Service}' is already stopped.", serviceName);
                            return;
                        }

                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);

                        if (sc.Status != ServiceControllerStatus.Stopped)
                        {
                            throw new WindowsIntegrationException($"Service '{serviceName}' failed to enter Stopped state within timeout.");
                        }
                    }
                }, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Service '{Service}' stopped successfully.", serviceName);
            }
            catch (System.ServiceProcess.TimeoutException ex)
            {
                throw new WindowsIntegrationException($"Timeout exceeded while waiting for service '{serviceName}' to stop.", ex);
            }
            catch (Exception ex) when (!(ex is WindowsIntegrationException || ex is OperationCanceledException))
            {
                throw new WindowsIntegrationException($"Failed to stop service '{serviceName}'.", ex);
            }
        }

        /// <inheritdoc />
        public async Task RestartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Attempting to restart service '{Service}'...", serviceName);

            var partialTimeout = TimeSpan.FromMilliseconds(timeout.TotalMilliseconds / 2.0);

            await StopServiceAsync(serviceName, partialTimeout, cancellationToken).ConfigureAwait(false);
            await StartServiceAsync(serviceName, partialTimeout, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Service '{Service}' restarted successfully.", serviceName);
        }

        private static WindowsServiceState MapServiceStatus(ServiceControllerStatus status)
        {
            return status switch
            {
                ServiceControllerStatus.Stopped => WindowsServiceState.Stopped,
                ServiceControllerStatus.StartPending => WindowsServiceState.StartPending,
                ServiceControllerStatus.StopPending => WindowsServiceState.StopPending,
                ServiceControllerStatus.Running => WindowsServiceState.Running,
                ServiceControllerStatus.ContinuePending => WindowsServiceState.ContinuePending,
                ServiceControllerStatus.PausePending => WindowsServiceState.PausePending,
                ServiceControllerStatus.Paused => WindowsServiceState.Paused,
                _ => WindowsServiceState.Unknown
            };
        }
    }
}
