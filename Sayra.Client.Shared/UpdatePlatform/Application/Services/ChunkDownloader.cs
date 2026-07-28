using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Downloads a specific byte range chunk from a designated mirror using streaming, bandwidth throttling, and exponential backoff.
    /// </summary>
    public class ChunkDownloader : IChunkDownloader
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IBandwidthLimiter _bandwidthLimiter;
        private readonly ILogger<ChunkDownloader> _logger;

        public ChunkDownloader(
            IHttpClientFactory httpClientFactory,
            IBandwidthLimiter bandwidthLimiter,
            ILogger<ChunkDownloader> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _bandwidthLimiter = bandwidthLimiter ?? throw new ArgumentNullException(nameof(bandwidthLimiter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task DownloadChunkAsync(
            DownloadChunk chunk,
            UpdatePackage package,
            MirrorEndpoint mirror,
            IProgressReporter progressReporter,
            CancellationToken cancellationToken = default)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (progressReporter == null) throw new ArgumentNullException(nameof(progressReporter));

            int attempt = 0;
            int maxRetries = 3;
            double baseDelaySeconds = 2;
            double maxDelaySeconds = 30;

            // Prepare directory
            string? dir = Path.GetDirectoryName(chunk.LocalFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempt++;

                try
                {
                    await ExecuteDownloadAsync(chunk, package, mirror, progressReporter, cancellationToken);
                    return; // Succeeded!
                }
                catch (Exception ex) when (IsTransientException(ex) && attempt <= maxRetries)
                {
                    _logger.LogWarning(ex, "Transient error downloading chunk {Index} on attempt {Attempt}. Retrying...", chunk.Index, attempt);

                    // Exponential Backoff with Jitter
                    // T_delay = 2^attempt * BaseDelay + Jitter
                    double delayVal = Math.Pow(2, attempt) * baseDelaySeconds;
                    double jitter = Random.Shared.NextDouble() * 1.5; // Up to 1.5s jitter
                    double totalDelaySeconds = Math.Min(delayVal + jitter, maxDelaySeconds);

                    await Task.Delay(TimeSpan.FromSeconds(totalDelaySeconds), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed downloading chunk {Index} of package {PackageId} after {Attempt} attempts.", chunk.Index, package.PackageId, attempt);
                    throw new ChunkDownloadException($"Failed to download chunk {chunk.Index} range offset {chunk.Offset}", ex);
                }
            }
        }

        private async Task ExecuteDownloadAsync(
            DownloadChunk chunk,
            UpdatePackage package,
            MirrorEndpoint mirror,
            IProgressReporter progressReporter,
            CancellationToken cancellationToken)
        {
            // Determine how many bytes we already have locally to support resuming within the chunk
            long existingBytes = 0;
            if (File.Exists(chunk.LocalFilePath))
            {
                existingBytes = new FileInfo(chunk.LocalFilePath).Length;
                if (existingBytes >= chunk.SizeBytes)
                {
                    // Chunk is already completed
                    chunk.BytesDownloaded = chunk.SizeBytes;
                    chunk.IsCompleted = true;
                    return;
                }
            }

            var client = _httpClientFactory.CreateClient("ChunkDownloaderClient");
            try
            {
                // Format download URI relative to the mirror, pointing to the specific package spk
                var requestUri = new Uri(mirror.BaseUri, $"{package.PackageId:D}.spk");

                var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

                long rangeStart = chunk.Offset + existingBytes;
                long rangeEnd = chunk.Offset + chunk.SizeBytes - 1;

                request.Headers.Range = new RangeHeaderValue(rangeStart, rangeEnd);

                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    if (response.StatusCode != HttpStatusCode.PartialContent && response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new HttpRequestException($"Server returned unexpected status code {response.StatusCode} for range {rangeStart}-{rangeEnd}");
                    }

                    using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                    {
                        // Open file for appending/writing
                        FileMode mode = existingBytes > 0 ? FileMode.Append : FileMode.Create;
                        using (var fileStream = new FileStream(chunk.LocalFilePath, mode, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                        {
                            byte[] buffer = new byte[8192];
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                // Enforce Bandwidth Limit
                                await _bandwidthLimiter.LimitAsync(bytesRead, cancellationToken);

                                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);

                                existingBytes += bytesRead;
                                chunk.BytesDownloaded = existingBytes;

                                // Report overall aggregated progress to the manager (will be tracked collectively)
                                // Triggering change events dynamically
                                progressReporter.ReportProgress(bytesRead);
                            }
                        }
                    }
                }
            }
            finally
            {
                // We should NOT dispose HttpClient if it is managed/disposed by IHttpClientFactory internally
            }

            if (existingBytes < chunk.SizeBytes)
            {
                throw new IOException($"Truncated chunk download. Expected {chunk.SizeBytes} bytes but got {existingBytes}");
            }

            chunk.IsCompleted = true;
        }

        private bool IsTransientException(Exception ex)
        {
            if (ex is HttpRequestException || ex is IOException || ex is TaskCanceledException)
            {
                if (ex is TaskCanceledException tce && !tce.CancellationToken.IsCancellationRequested)
                {
                    // Timeout
                    return true;
                }
                return true;
            }
            return false;
        }
    }
}
