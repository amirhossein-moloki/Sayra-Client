using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Dtos;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Thread-safe enterprise UpdateManager that orchestrates checking, downloading, and installing update pipelines.
    /// </summary>
    public class UpdateManager : IUpdateManager
    {
        private readonly object _stateLock = new object();
        private readonly IDownloadManager _downloadManager;
        private readonly IPackageVerifier _packageVerifier;
        private readonly IInstallerEngine _installerEngine;
        private UpdateState _currentState = UpdateState.Idle;
        private CancellationTokenSource? _activeCts;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateManager"/> class.
        /// </summary>
        public UpdateManager(
            IDownloadManager downloadManager,
            IPackageVerifier packageVerifier,
            IInstallerEngine installerEngine)
        {
            _downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
            _packageVerifier = packageVerifier ?? throw new ArgumentNullException(nameof(packageVerifier));
            _installerEngine = installerEngine ?? throw new ArgumentNullException(nameof(installerEngine));
        }

        /// <inheritdoc />
        public UpdateState GetCurrentState()
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }

        /// <inheritdoc />
        public Task<UpdateCheckResponseDto> CheckForUpdatesAsync(UpdateCheckRequestDto request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            lock (_stateLock)
            {
                if (_currentState != UpdateState.Idle)
                {
                    throw new UpdateException("An update check or installation is already in progress.");
                }
                _currentState = UpdateState.Checking;
            }

            try
            {
                // Simple default response for update pollers
                var result = new UpdateCheckResponseDto
                {
                    UpdateAvailable = false
                };
                return Task.FromResult(result);
            }
            finally
            {
                lock (_stateLock)
                {
                    _currentState = UpdateState.Idle;
                }
            }
        }

        /// <inheritdoc />
        public async Task<bool> StartUpdateAsync(UpdateManifest manifest, CancellationToken cancellationToken = default)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            lock (_stateLock)
            {
                if (_currentState != UpdateState.Idle)
                {
                    throw new UpdateException("Cannot start update because another operation is in progress.");
                }
                _currentState = UpdateState.Downloading;
                _activeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

            CancellationToken linkedToken = _activeCts.Token;

            try
            {
                var package = new UpdatePackage
                {
                    PackageId = manifest.Id,
                    Version = manifest.Version,
                    PackageType = manifest.PackageType
                };

                // 1. Download Package
                linkedToken.ThrowIfCancellationRequested();
                string packagePath = await _downloadManager.DownloadAsync(package, linkedToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(packagePath) || !File.Exists(packagePath))
                {
                    throw new DownloadFailedException($"Failed to download update package: Path is invalid.");
                }

                // 2. Verify Package
                lock (_stateLock)
                {
                    _currentState = UpdateState.Verifying;
                }
                linkedToken.ThrowIfCancellationRequested();
                bool isVerified = await _packageVerifier.VerifyPackageIntegrityAsync(packagePath, package, linkedToken).ConfigureAwait(false);
                if (!isVerified)
                {
                    throw new PackageCorruptedException("Package verification failed.");
                }

                // 3. Install Package
                lock (_stateLock)
                {
                    _currentState = UpdateState.Installing;
                }
                linkedToken.ThrowIfCancellationRequested();
                bool installSuccess = await _installerEngine.InstallAsync(package, linkedToken).ConfigureAwait(false);
                if (!installSuccess)
                {
                    throw new InstallationFailedException("Package installation failed.");
                }

                lock (_stateLock)
                {
                    _currentState = UpdateState.Completed;
                }
                return true;
            }
            catch (OperationCanceledException)
            {
                lock (_stateLock)
                {
                    _currentState = UpdateState.Cancelled;
                }
                return false;
            }
            catch (Exception)
            {
                lock (_stateLock)
                {
                    _currentState = UpdateState.Failed;
                }
                throw;
            }
            finally
            {
                lock (_stateLock)
                {
                    _activeCts?.Dispose();
                    _activeCts = null;
                }
            }
        }

        /// <inheritdoc />
        public Task CancelUpdateAsync(CancellationToken cancellationToken = default)
        {
            lock (_stateLock)
            {
                _activeCts?.Cancel();
            }
            return Task.CompletedTask;
        }
    }
}
