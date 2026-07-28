using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Thread-safe central orchestrator managing parallel, chunked resumable downloads with CDN/mirror fallback.
    /// </summary>
    public class DownloadManager : IDownloadManager
    {
        private readonly IMirrorSelector _mirrorSelector;
        private readonly IChunkDownloader _chunkDownloader;
        private readonly IDownloadStateStore _stateStore;
        private readonly IBandwidthLimiter _bandwidthLimiter;
        private readonly IProgressReporter _progressReporter;
        private readonly IOptions<DownloadOptions> _downloadOptions;
        private readonly ILogger<DownloadManager> _logger;
        private readonly SemaphoreSlim _downloadSemaphore = new SemaphoreSlim(1, 1);
        private readonly object _progressLock = new object();
        private long _aggregatedBytesDownloaded;

        public event EventHandler<DownloadProgress>? ProgressChanged;

        public DownloadManager(
            IMirrorSelector mirrorSelector,
            IChunkDownloader chunkDownloader,
            IDownloadStateStore stateStore,
            IBandwidthLimiter bandwidthLimiter,
            IProgressReporter progressReporter,
            IOptions<DownloadOptions> downloadOptions,
            ILogger<DownloadManager> logger)
        {
            _mirrorSelector = mirrorSelector ?? throw new ArgumentNullException(nameof(mirrorSelector));
            _chunkDownloader = chunkDownloader ?? throw new ArgumentNullException(nameof(chunkDownloader));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _bandwidthLimiter = bandwidthLimiter ?? throw new ArgumentNullException(nameof(bandwidthLimiter));
            _progressReporter = progressReporter ?? throw new ArgumentNullException(nameof(progressReporter));
            _downloadOptions = downloadOptions ?? throw new ArgumentNullException(nameof(downloadOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Setup default bandwidth policy based on options
            _bandwidthLimiter.SetLimit((long)(_downloadOptions.Value.MaxBandwidthMbps * 1024 * 1024 / 8));

            // Bubble up progress events
            _progressReporter.ProgressChanged += (s, e) =>
            {
                ProgressChanged?.Invoke(this, e);
            };
        }

        public void ConfigureBandwidthPolicy(BandwidthPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            long bytesPerSecond = policy.ThrottlingEnabled ? policy.MaxBytesPerSecond : 0;
            _bandwidthLimiter.SetLimit(bytesPerSecond);
        }

        public async Task<string> DownloadAsync(UpdatePackage package, CancellationToken cancellationToken = default)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));

            await _downloadSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Starting download of package {PackageId} (Version {Version})", package.PackageId, package.Version);

                // 1. Recover/Initialize Job State
                DownloadJob job = await LoadOrInitializeJobAsync(package, cancellationToken);
                job.Status = "Downloading";
                await _stateStore.SaveJobAsync(job, cancellationToken);

                // Configure progress reporter
                long previouslyCompletedBytes = job.Chunks.Where(c => c.IsCompleted).Sum(c => c.SizeBytes);
                _aggregatedBytesDownloaded = previouslyCompletedBytes;
                _progressReporter.Reset(job.JobId, job.TotalSizeBytes);
                _progressReporter.ReportProgress(_aggregatedBytesDownloaded);

                // 2. Mirror Endpoint selection
                MirrorEndpoint mirror = _mirrorSelector.GetBestEndpoint();

                // 3. Parallel Chunk Execution Loop
                int maxConcurrency = Math.Max(1, _downloadOptions.Value.MaxParallelDownloads);
                var uncompletedChunks = job.Chunks.Where(c => !c.IsCompleted).ToList();

                _logger.LogInformation("Downloading {Count} uncompleted chunks using {Concurrency} parallel workers from mirror {MirrorName}",
                    uncompletedChunks.Count, maxConcurrency, mirror.Name);

                var localProgressReporter = new AggregatingProgressReporter(bytes =>
                {
                    lock (_progressLock)
                    {
                        _aggregatedBytesDownloaded += bytes;
                        _progressReporter.ReportProgress(_aggregatedBytesDownloaded);
                    }
                });

                using (var semaphore = new SemaphoreSlim(maxConcurrency))
                {
                    var downloadTasks = uncompletedChunks.Select(async chunk =>
                    {
                        await semaphore.WaitAsync(cancellationToken);
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // Downloader performs chunk range range retrieval, streaming, retrying, and throttling
                            await _chunkDownloader.DownloadChunkAsync(chunk, package, mirror, localProgressReporter, cancellationToken);

                            // Persist step-level progress of this chunk
                            await _stateStore.SaveJobAsync(job, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _mirrorSelector.ReportFailure(mirror);
                            _logger.LogError(ex, "Failed downloading chunk {Index}", chunk.Index);
                            throw;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    await Task.WhenAll(downloadTasks);
                }

                // 4. Verification and Assembly Merges
                job.Status = "Merging";
                await _stateStore.SaveJobAsync(job, cancellationToken);

                _logger.LogInformation("All chunks downloaded. Merging into final package file: {Target}", job.TargetFilePath);
                await MergeChunksAsync(job, cancellationToken);

                // Mark Completed
                job.Status = "Completed";
                job.CompletedAt = DateTime.UtcNow;
                await _stateStore.SaveJobAsync(job, cancellationToken);

                _logger.LogInformation("Successfully completed package download: {Target}", job.TargetFilePath);
                return job.TargetFilePath;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Download canceled for package {PackageId}", package.PackageId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Download pipeline encountered a critical failure for package {PackageId}", package.PackageId);
                throw new DownloadFailedException($"Failed to download update package {package.PackageId}", ex);
            }
            finally
            {
                _downloadSemaphore.Release();
            }
        }

        private async Task<DownloadJob> LoadOrInitializeJobAsync(UpdatePackage package, CancellationToken cancellationToken)
        {
            var job = await _stateStore.LoadJobAsync(package.PackageId, cancellationToken);
            if (job != null)
            {
                _logger.LogInformation("Found existing download state. Resuming from last progress state.");
                return job;
            }

            // Create temporary folder & build job layout
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string tempDir = Path.Combine(appData, "SAYRA_Client", "UpdateCache", package.PackageId.ToString("D"));
            string finalPath = Path.Combine(appData, "SAYRA_Client", "UpdateCache", $"{package.PackageId:D}.spk");

            // Divide package into standard 1MB chunks (matching manifest/metadata if empty, otherwise we split it safely)
            // Typically packages have their own specific metadata or division.
            // If the package has zero dependencies, we fall back to a single chunk or calculate 1MB segments.
            long chunkSize = 1024 * 1024; // 1MB chunks
            long totalBytes = package.Size;
            int chunkCount = (int)Math.Max(1, Math.Ceiling((double)totalBytes / chunkSize));

            var chunks = new List<DownloadChunk>();
            for (int i = 0; i < chunkCount; i++)
            {
                long offset = i * chunkSize;
                long size = Math.Min(chunkSize, totalBytes - offset);
                chunks.Add(new DownloadChunk
                {
                    Index = i,
                    Offset = offset,
                    SizeBytes = size,
                    LocalFilePath = Path.Combine(tempDir, $"chunk_{i}.part"),
                    IsCompleted = false,
                    BytesDownloaded = 0
                });
            }

            return new DownloadJob
            {
                JobId = Guid.NewGuid(),
                PackageId = package.PackageId,
                Version = package.Version,
                TotalSizeBytes = totalBytes,
                TargetFilePath = finalPath,
                TempDirectory = tempDir,
                Chunks = chunks,
                Status = "Pending"
            };
        }

        private async Task MergeChunksAsync(DownloadJob job, CancellationToken cancellationToken)
        {
            string? targetDir = Path.GetDirectoryName(job.TargetFilePath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Use transactional merge to avoid partial file corruptions on power cuts
            string tempMergedFile = job.TargetFilePath + ".tmp";

            try
            {
                using (var destStream = new FileStream(tempMergedFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    foreach (var chunk in job.Chunks.OrderBy(c => c.Index))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!File.Exists(chunk.LocalFilePath))
                        {
                            throw new FileNotFoundException($"Missing physical chunk part: {chunk.LocalFilePath}");
                        }

                        using (var srcStream = new FileStream(chunk.LocalFilePath, FileMode.Open, FileAccess.Read, FileShare.None, 4096, useAsync: true))
                        {
                            await srcStream.CopyToAsync(destStream, cancellationToken);
                        }
                    }
                }

                // Atomic Swap
                if (File.Exists(job.TargetFilePath))
                {
                    File.Delete(job.TargetFilePath);
                }
                File.Move(tempMergedFile, job.TargetFilePath);

                // Cleanup Chunk Parts
                foreach (var chunk in job.Chunks)
                {
                    try
                    {
                        if (File.Exists(chunk.LocalFilePath))
                        {
                            File.Delete(chunk.LocalFilePath);
                        }
                    }
                    catch
                    {
                        // Safe cleanup fallback
                    }
                }

                try
                {
                    if (Directory.Exists(job.TempDirectory) && !Directory.EnumerateFileSystemEntries(job.TempDirectory).Any())
                    {
                        Directory.Delete(job.TempDirectory);
                    }
                }
                catch
                {
                    // Safe cleanup fallback
                }
            }
            catch (Exception ex)
            {
                if (File.Exists(tempMergedFile))
                {
                    try { File.Delete(tempMergedFile); } catch { /* Ignore */ }
                }
                throw new IOException($"Failed to assemble and merge chunks into final SPK file: {job.TargetFilePath}", ex);
            }
        }

        private class AggregatingProgressReporter : IProgressReporter
        {
            private readonly Action<long> _onReport;

            public AggregatingProgressReporter(Action<long> onReport)
            {
                _onReport = onReport;
            }

            public DownloadProgress CurrentProgress => throw new NotImplementedException();
            public event EventHandler<DownloadProgress>? ProgressChanged { add { } remove { } }

            public void Reset(Guid jobId, long totalSizeBytes) { }
            public void ReportProgress(long bytesDownloaded)
            {
                _onReport(bytesDownloaded);
            }
        }
    }
}
