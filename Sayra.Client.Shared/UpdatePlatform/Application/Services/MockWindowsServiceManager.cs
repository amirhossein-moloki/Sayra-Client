using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Mock implementation of the Windows Service Control Manager (SCM) wrapper for test-only and cross-platform (Linux CI) environments.
    /// </summary>
    public class MockWindowsServiceManager : IWindowsServiceManager
    {
        private readonly ILogger<MockWindowsServiceManager> _logger;
        private readonly ConcurrentDictionary<string, WindowsServiceState> _mockStates = new();
        private readonly ConcurrentDictionary<string, bool> _mockDisabled = new();

        public MockWindowsServiceManager(ILogger<MockWindowsServiceManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Mock helper to simulate a disabled service state for tests.
        /// </summary>
        public void SetMockServiceDisabled(string serviceName, bool disabled)
        {
            _mockDisabled[serviceName] = disabled;
        }

        /// <inheritdoc />
        public Task<WindowsServiceState> GetServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentException("Service name cannot be empty.", nameof(serviceName));

            return Task.FromResult(_mockStates.GetOrAdd(serviceName, WindowsServiceState.Stopped));
        }

        /// <inheritdoc />
        public async Task StartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentException("Service name cannot be empty.", nameof(serviceName));

            if (_mockDisabled.TryGetValue(serviceName, out bool disabled) && disabled)
            {
                throw new WindowsIntegrationException($"Service '{serviceName}' is disabled and cannot be started.");
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            _mockStates[serviceName] = WindowsServiceState.Running;
        }

        /// <inheritdoc />
        public async Task StopServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentException("Service name cannot be empty.", nameof(serviceName));

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            _mockStates[serviceName] = WindowsServiceState.Stopped;
        }

        /// <inheritdoc />
        public async Task RestartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var partialTimeout = TimeSpan.FromMilliseconds(timeout.TotalMilliseconds / 2.0);
            await StopServiceAsync(serviceName, partialTimeout, cancellationToken).ConfigureAwait(false);
            await StartServiceAsync(serviceName, partialTimeout, cancellationToken).ConfigureAwait(false);
        }
    }
}
