using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Coordinates the full installation pipeline utilizing the state machine, atomic replacer, restart manager, and validator.
    /// Thread-safe execution and cancellation-aware.
    /// </summary>
    public class InstallationCoordinator : IInstallationCoordinator
    {
        private readonly Func<IInstallationStateMachine> _stateMachineFactory;
        private readonly IRestartManagerService _restartManager;
        private readonly IAtomicFileReplacer _fileReplacer;
        private readonly IInstallationValidator _validator;
        private readonly SemaphoreSlim _concurrencyLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationCoordinator"/> class.
        /// </summary>
        public InstallationCoordinator(
            Func<IInstallationStateMachine> stateMachineFactory,
            IRestartManagerService restartManager,
            IAtomicFileReplacer fileReplacer,
            IInstallationValidator validator)
        {
            _stateMachineFactory = stateMachineFactory ?? throw new ArgumentNullException(nameof(stateMachineFactory));
            _restartManager = restartManager ?? throw new ArgumentNullException(nameof(restartManager));
            _fileReplacer = fileReplacer ?? throw new ArgumentNullException(nameof(fileReplacer));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        /// <inheritdoc />
        public async Task<InstallationResult> CoordinateAsync(InstallationJob job, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            // Prevent concurrent installations
            if (!await _concurrencyLock.WaitAsync(0, cancellationToken))
            {
                return InstallationResult.Failed("Another installation operation is currently running.");
            }

            string stagingDir = Path.Combine(Path.GetTempPath(), $"SAYRA_Staging_{job.Id}");
            string backupDir = Path.Combine(Path.GetTempPath(), $"SAYRA_Backup_{job.Id}");
            string targetDir = AppContext.BaseDirectory; // Standard target is the application's base directory

            // Instantiate a fresh state machine per-installation to prevent captive dependency issues
            IInstallationStateMachine stateMachine = _stateMachineFactory();

            try
            {
                // Ensure directories exist
                Directory.CreateDirectory(stagingDir);
                Directory.CreateDirectory(backupDir);

                var context = new InstallationContext(job, stagingDir, targetDir, backupDir, cancellationToken, progress);

                // Step 1: Preparing
                UpdateProgress(progress, job, stateMachine, 10, InstallationState.Preparing);

                // Step 2: Validating
                UpdateProgress(progress, job, stateMachine, 20, InstallationState.Validating);
                ValidatePackageFile(context);

                // Step 3: Staging (Unpack update package to staging directory)
                UpdateProgress(progress, job, stateMachine, 40, InstallationState.Staging);
                ExtractPackageToStaging(context);

                // Populate staged files inventory and compute pre-install hashes
                await RecordStagedFilesInventoryAsync(context).ConfigureAwait(false);

                // Step 4: StoppingServices (Close locked applications/services gracefully)
                UpdateProgress(progress, job, stateMachine, 60, InstallationState.StoppingServices);
                _restartManager.ShutdownApplications(Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories));

                // Step 5: Installing (Atomic replace files)
                UpdateProgress(progress, job, stateMachine, 80, InstallationState.Installing);
                _fileReplacer.ReplaceDirectoryContents(context.StagingDirectory, context.TargetDirectory, context.BackupDirectory);

                // Step 6: Verifying (Post-install check)
                UpdateProgress(progress, job, stateMachine, 90, InstallationState.Verifying);
                bool isValid = await _validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
                if (!isValid)
                {
                    throw new InstallationValidationException("Post-installation file integrity check failed.");
                }

                // Step 7: Restarting (Restart services/processes)
                UpdateProgress(progress, job, stateMachine, 95, InstallationState.Restarting);
                _restartManager.RestartApplications();

                // Step 8: Completed
                UpdateProgress(progress, job, stateMachine, 100, InstallationState.Completed);

                return InstallationResult.Successful(restartRequired: true);
            }
            catch (OperationCanceledException)
            {
                UpdateProgress(progress, job, stateMachine, 100, InstallationState.Failed, "Installation was cancelled.");
                return InstallationResult.Failed("Installation was cancelled.");
            }
            catch (Exception ex)
            {
                UpdateProgress(progress, job, stateMachine, 100, InstallationState.Failed, ex.Message);
                return InstallationResult.Failed(ex.Message);
            }
            finally
            {
                _concurrencyLock.Release();

                // Safe clean up staging directories
                SafeDeleteDirectory(stagingDir);
                SafeDeleteDirectory(backupDir);
            }
        }

        private void UpdateProgress(IProgress<double>? progress, InstallationJob job, IInstallationStateMachine stateMachine, double percentage, InstallationState state, string? errorMessage = null)
        {
            job.ProgressPercentage = percentage;
            job.ErrorMessage = errorMessage;
            if (job.State != state)
            {
                stateMachine.TransitionTo(state);
                job.State = state;
            }
            progress?.Report(percentage);
        }

        private void ValidatePackageFile(InstallationContext context)
        {
            if (string.IsNullOrEmpty(context.Job.PackagePath))
            {
                throw new InstallationValidationException("No update package file path specified.");
            }

            if (!File.Exists(context.Job.PackagePath))
            {
                throw new FileNotFoundException($"Update package file '{context.Job.PackagePath}' not found.", context.Job.PackagePath);
            }
        }

        private void ExtractPackageToStaging(InstallationContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                string stagingRoot = Path.GetFullPath(context.StagingDirectory);
                if (!stagingRoot.EndsWith(Path.DirectorySeparatorChar))
                {
                    stagingRoot += Path.DirectorySeparatorChar;
                }

                // In production, package can be zip-compressed.
                // Let's support standard ZIP extraction for .zip or .spk (if zip-compatible)
                using ZipArchive archive = ZipFile.OpenRead(context.Job.PackagePath);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();

                    string destinationPath = Path.GetFullPath(Path.Combine(context.StagingDirectory, entry.FullName));

                    // Avoid path traversal (Zip Slip) attacks (ensure target starts with staging root trailing separator)
                    if (!destinationPath.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InstallationFailedException($"Malicious file path detected in package: {entry.FullName}");
                    }

                    string? directory = Path.GetDirectoryName(destinationPath);
                    if (directory != null && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        entry.ExtractToFile(destinationPath, true);
                    }
                }
            }
            catch (InvalidDataException)
            {
                // Fallback for non-zip testing or binary files: copy file directly to staging
                string destFile = Path.Combine(context.StagingDirectory, Path.GetFileName(context.Job.PackagePath));
                File.Copy(context.Job.PackagePath, destFile, true);
            }
            catch (Exception ex) when (!(ex is InstallationFailedException || ex is OperationCanceledException))
            {
                throw new InstallationFailedException($"Failed to extract update package to staging area.", ex);
            }
        }

        private async Task RecordStagedFilesInventoryAsync(InstallationContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(context.StagingDirectory))
            {
                return;
            }

            string[] files = Directory.GetFiles(context.StagingDirectory, "*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                string relativePath = Path.GetRelativePath(context.StagingDirectory, file);
                string hash = await ComputeSha256Async(file, context.CancellationToken).ConfigureAwait(false);
                context.Job.StagedFiles[relativePath] = hash;
            }
        }

        private async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

            byte[] buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            }
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return BitConverter.ToString(sha256.Hash!).Replace("-", "").ToLowerInvariant();
        }

        private void SafeDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // Suppress cleanup errors to prevent hiding primary exceptions
            }
        }
    }
}
