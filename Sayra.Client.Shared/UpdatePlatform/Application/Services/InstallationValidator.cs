using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Performs robust post-installation validation of file integrity, manifest consistency, and version correctness.
    /// </summary>
    public class InstallationValidator : IInstallationValidator
    {
        /// <inheritdoc />
        public async Task<bool> ValidateAsync(InstallationContext context, CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // 1. Verify Manifest Consistency and Version Correctness
                if (string.IsNullOrWhiteSpace(context.Job.Package.Version))
                {
                    throw new InstallationValidationException("Installation job package version cannot be empty.");
                }

                if (!Directory.Exists(context.TargetDirectory))
                {
                    throw new InstallationValidationException($"Target directory {context.TargetDirectory} does not exist.");
                }

                // 2. Validate using the StagedFiles dictionary (populated pre-installation)
                if (context.Job.StagedFiles != null && context.Job.StagedFiles.Count > 0)
                {
                    foreach (var pair in context.Job.StagedFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string relativePath = pair.Key;
                        string expectedHash = pair.Value;

                        if (IsConfigurationOrPersistentFile(relativePath))
                        {
                            continue;
                        }

                        string targetFile = Path.Combine(context.TargetDirectory, relativePath);

                        if (!File.Exists(targetFile))
                        {
                            throw new InstallationValidationException($"Required file '{relativePath}' is missing from target directory post-installation.");
                        }

                        string targetHash = await ComputeSha256Async(targetFile, cancellationToken).ConfigureAwait(false);
                        if (!string.Equals(expectedHash, targetHash, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InstallationValidationException($"File integrity violation: Hash mismatch for '{relativePath}'. Expected: {expectedHash}, Target: {targetHash}");
                        }
                    }
                    return true;
                }

                // Fallback validation: if StagedFiles dictionary was not populated, check active staging folder files
                if (!Directory.Exists(context.StagingDirectory))
                {
                    throw new InstallationValidationException($"Staging directory {context.StagingDirectory} does not exist.");
                }

                string[] stagedFiles = Directory.GetFiles(context.StagingDirectory, "*", SearchOption.AllDirectories);
                if (stagedFiles.Length == 0)
                {
                    throw new InstallationValidationException("Staging directory is empty. Nothing was staged for installation.");
                }

                foreach (string stagedFile in stagedFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string relativePath = Path.GetRelativePath(context.StagingDirectory, stagedFile);

                    if (IsConfigurationOrPersistentFile(relativePath))
                    {
                        continue;
                    }

                    string targetFile = Path.Combine(context.TargetDirectory, relativePath);

                    if (!File.Exists(targetFile))
                    {
                        throw new InstallationValidationException($"Required file '{relativePath}' is missing from target directory post-installation.");
                    }

                    string stagedHash = await ComputeSha256Async(stagedFile, cancellationToken).ConfigureAwait(false);
                    string targetHash = await ComputeSha256Async(targetFile, cancellationToken).ConfigureAwait(false);

                    if (!string.Equals(stagedHash, targetHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InstallationValidationException($"File integrity violation: Hash mismatch for '{relativePath}'. Staged: {stagedHash}, Target: {targetHash}");
                    }
                }

                return true;
            }
            catch (Exception ex) when (!(ex is InstallationValidationException))
            {
                throw new InstallationValidationException("Post-installation validation failed due to an unexpected error.", ex);
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

        private bool IsConfigurationOrPersistentFile(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return false;

            string normalized = relativePath.Replace('\\', '/').ToLowerInvariant();

            return normalized.Contains("client_config.json") ||
                   normalized.Contains("db_key.bin") ||
                   normalized.EndsWith(".db") ||
                   normalized.Contains("databases/") ||
                   normalized.Contains("logs/") ||
                   normalized.EndsWith(".log");
        }
    }
}
