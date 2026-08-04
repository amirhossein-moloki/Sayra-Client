using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Models.Phase9.Options;
using Sayra.Client.Shared.Fleet.Diagnostics.Interfaces;

namespace Sayra.Client.Shared.Fleet.Diagnostics.Services
{
    /// <summary>
    /// Thread-safe local file-based implementation of <see cref="IDiagnosticStorage"/>,
    /// enforcing quotas and retention cleanups automatically.
    /// </summary>
    public class DiagnosticStorage : IDiagnosticStorage
    {
        private readonly IOptionsMonitor<DiagnosticsOptions> _options;
        private readonly ILogger<DiagnosticStorage> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticStorage"/> class.
        /// </summary>
        public DiagnosticStorage(
            IOptionsMonitor<DiagnosticsOptions> options,
            ILogger<DiagnosticStorage> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            EnsureStagingDirExists();
        }

        private string GetStagingDir()
        {
            var path = _options.CurrentValue.LocalStagingDirectory;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "Data/Diagnostics";
            }
            return path;
        }

        private void EnsureStagingDirExists()
        {
            try
            {
                var dir = GetStagingDir();
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create diagnostics staging directory.");
            }
        }

        /// <inheritdoc />
        public async Task SavePackageAsync(string packageId, byte[] packageData, string fileName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(packageId)) throw new ArgumentException("PackageId cannot be null or empty.", nameof(packageId));
            if (packageData == null) throw new ArgumentNullException(nameof(packageData));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("FileName cannot be null or empty.", nameof(fileName));

            await _lock.WaitAsync(ct);
            try
            {
                EnsureStagingDirExists();
                var dir = GetStagingDir();
                var filePath = Path.Combine(dir, $"{packageId}_{fileName}");

                _logger.LogInformation("Saving diagnostics package {PackageId} to {FilePath}...", packageId, filePath);

                // Atomic write operation
                var tempPath = filePath + ".tmp";
                await File.WriteAllBytesAsync(tempPath, packageData, ct);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                File.Move(tempPath, filePath);

                _logger.LogInformation("Diagnostics package {PackageId} saved successfully.", packageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while saving diagnostics package {PackageId}.", packageId);
                throw;
            }
            finally
            {
                _lock.Release();
            }

            // Resilient storage audit
            await EnforceCleanupPolicyAsync(ct);
        }

        /// <inheritdoc />
        public async Task<byte[]?> GetPackageAsync(string packageId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(packageId)) throw new ArgumentException("PackageId cannot be null or empty.", nameof(packageId));

            await _lock.WaitAsync(ct);
            try
            {
                var filePath = await ResolvePathInternalAsync(packageId);
                if (filePath == null || !File.Exists(filePath))
                {
                    _logger.LogWarning("Diagnostics package {PackageId} not found in storage.", packageId);
                    return null;
                }

                return await File.ReadAllBytesAsync(filePath, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read diagnostics package {PackageId} from storage.", packageId);
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<string> GetPackagePathAsync(string packageId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(packageId)) throw new ArgumentException("PackageId cannot be null or empty.", nameof(packageId));

            await _lock.WaitAsync(ct);
            try
            {
                var filePath = await ResolvePathInternalAsync(packageId);
                if (filePath == null || !File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Diagnostics package {packageId} was not found in storage.");
                }
                return filePath;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task DeletePackageAsync(string packageId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(packageId)) throw new ArgumentException("PackageId cannot be null or empty.", nameof(packageId));

            await _lock.WaitAsync(ct);
            try
            {
                var filePath = await ResolvePathInternalAsync(packageId);
                if (filePath != null && File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Successfully deleted diagnostics package {PackageId} from storage.", packageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete diagnostics package {PackageId}.", packageId);
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task EnforceCleanupPolicyAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                EnsureStagingDirExists();
                var dir = GetStagingDir();
                var maxMb = _options.CurrentValue.MaxDiagnosticsStorageMb;
                if (maxMb <= 0) return;

                long maxBytes = maxMb * 1024 * 1024;
                var files = Directory.GetFiles(dir)
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.LastWriteTimeUtc)
                    .ToList();

                long currentBytes = files.Sum(f => f.Length);
                if (currentBytes <= maxBytes) return;

                _logger.LogInformation("Diagnostics storage ceiling exceeded (Current: {CurrentMb}MB, MaxAllowed: {MaxMb}MB). Enforcing cleanup policy...",
                    currentBytes / (1024.0 * 1024.0), maxMb);

                foreach (var file in files)
                {
                    if (currentBytes <= maxBytes) break;

                    try
                    {
                        var len = file.Length;
                        file.Delete();
                        currentBytes -= len;
                        _logger.LogInformation("Pruned old diagnostics package: {FileName}", file.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete old file {FileName} during cleanup.", file.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enforcing diagnostics storage cleanup policy.");
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public async Task ClearExpiredPackagesAsync(TimeSpan expiration, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                EnsureStagingDirExists();
                var dir = GetStagingDir();
                var cutoff = DateTime.UtcNow - expiration;

                var files = Directory.GetFiles(dir)
                    .Select(f => new FileInfo(f))
                    .Where(f => f.LastWriteTimeUtc < cutoff)
                    .ToList();

                if (files.Count == 0) return;

                _logger.LogInformation("Clearing {Count} expired diagnostics package(s) older than {Cutoff}...", files.Count, cutoff);

                foreach (var file in files)
                {
                    try
                    {
                        file.Delete();
                        _logger.LogInformation("Cleared expired diagnostics package: {FileName}", file.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete expired diagnostics file {FileName}.", file.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing expired diagnostics packages.");
            }
            finally
            {
                _lock.Release();
            }
        }

        private Task<string?> ResolvePathInternalAsync(string packageId)
        {
            EnsureStagingDirExists();
            var dir = GetStagingDir();
            var files = Directory.GetFiles(dir);

            // Match files starting with "{packageId}_"
            var prefix = $"{packageId}_";
            var matchedFile = files.FirstOrDefault(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(matchedFile);
        }
    }
}
