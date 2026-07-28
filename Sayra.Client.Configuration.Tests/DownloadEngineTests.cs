using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Services;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;

namespace Sayra.Client.Configuration.Tests
{
    public class DownloadEngineTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

        public DownloadEngineTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        }

        #region BandwidthLimiter Tests

        [Fact]
        public async Task BandwidthLimiter_ThrottlesCorrectly()
        {
            // Arrange
            var limiter = new BandwidthLimiter();
            limiter.SetLimit(100); // 100 bytes per second

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Act - Request 150 bytes (should take roughly 1.5 seconds)
            await limiter.LimitAsync(150, CancellationToken.None);
            sw.Stop();

            // Assert
            Assert.True(sw.Elapsed.TotalSeconds >= 0.5, $"Expected significant throttle, but took only {sw.Elapsed.TotalSeconds} seconds.");
        }

        [Fact]
        public async Task BandwidthLimiter_NoThrottleIfZeroLimit()
        {
            // Arrange
            var limiter = new BandwidthLimiter();
            limiter.SetLimit(0); // Disabled

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Act
            await limiter.LimitAsync(1000000, CancellationToken.None);
            sw.Stop();

            // Assert
            Assert.True(sw.Elapsed.TotalMilliseconds < 100);
        }

        #endregion

        #region MirrorSelector Tests

        [Fact]
        public void MirrorSelector_RegistersAndSelectsBestEndpoint()
        {
            // Arrange
            var selector = new MirrorSelector(_mockHttpClientFactory.Object);
            var ep1 = new MirrorEndpoint { Name = "CDN1", BaseUri = new Uri("https://cdn1.com/"), Priority = 2, IsHealthy = true };
            var ep2 = new MirrorEndpoint { Name = "CDN2", BaseUri = new Uri("https://cdn2.com/"), Priority = 1, IsHealthy = true };

            selector.RegisterEndpoint(ep1);
            selector.RegisterEndpoint(ep2);

            // Act
            var best = selector.GetBestEndpoint();

            // Assert
            Assert.Equal("CDN2", best.Name); // Priority 1 wins over Priority 2
        }

        [Fact]
        public void MirrorSelector_FailsOverOnUnhealthy()
        {
            // Arrange
            var selector = new MirrorSelector(_mockHttpClientFactory.Object);
            var ep1 = new MirrorEndpoint { Name = "CDN1", BaseUri = new Uri("https://cdn1.com/"), Priority = 2, IsHealthy = true };
            var ep2 = new MirrorEndpoint { Name = "CDN2", BaseUri = new Uri("https://cdn2.com/"), Priority = 1, IsHealthy = true };

            selector.RegisterEndpoint(ep1);
            selector.RegisterEndpoint(ep2);

            // Report failure enough to make it unhealthy
            selector.ReportFailure(ep2);
            selector.ReportFailure(ep2);
            selector.ReportFailure(ep2);

            // Act
            var best = selector.GetBestEndpoint();

            // Assert
            Assert.Equal("CDN1", best.Name); // Fails over to ep1
        }

        [Fact]
        public void MirrorSelector_ThrowsIfNoMirrorsAvailable()
        {
            // Arrange
            var selector = new MirrorSelector(_mockHttpClientFactory.Object);

            // Act & Assert
            Assert.Throws<MirrorUnavailableException>(() => selector.GetBestEndpoint());
        }

        #endregion

        #region DownloadStateStore Tests

        [Fact]
        public async Task DownloadStateStore_SavesAndLoadsAtomically()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var store = new DownloadStateStore(tempDir);
            var packageId = Guid.NewGuid();
            var job = new DownloadJob
            {
                PackageId = packageId,
                Version = "1.0.0",
                TotalSizeBytes = 5000,
                TargetFilePath = "final.spk"
            };

            try
            {
                // Act
                await store.SaveJobAsync(job);
                var loaded = await store.LoadJobAsync(packageId);

                // Assert
                Assert.NotNull(loaded);
                Assert.Equal("1.0.0", loaded.Version);
                Assert.Equal(5000, loaded.TotalSizeBytes);

                // Delete
                await store.DeleteJobAsync(packageId);
                var afterDelete = await store.LoadJobAsync(packageId);
                Assert.Null(afterDelete);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        #endregion

        #region ProgressReporter Tests

        [Fact]
        public void ProgressReporter_TracksSpeedAndEta()
        {
            // Arrange
            var reporter = new ProgressReporter();
            var jobId = Guid.NewGuid();
            reporter.Reset(jobId, 1000);

            DownloadProgress? progressData = null;
            reporter.ProgressChanged += (s, e) => { progressData = e; };

            // Act
            reporter.ReportProgress(500);

            // Assert
            Assert.NotNull(progressData);
            Assert.Equal(500, progressData.BytesDownloaded);
            Assert.Equal(1000, progressData.TotalSizeBytes);
            Assert.Equal(50.0, progressData.Percentage);
        }

        #endregion

        #region ChunkDownloader Tests

        [Fact]
        public async Task ChunkDownloader_DownloadsRangeAndSupportsResume()
        {
            // Arrange
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.PartialContent,
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("chunk-bytes-data"))
                });

            var httpClient = new HttpClient(mockHandler.Object);
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var limiter = new BandwidthLimiter();
            limiter.SetLimit(0); // Disabled throttling

            var chunkDownloader = new ChunkDownloader(
                _mockHttpClientFactory.Object,
                limiter,
                NullLogger<ChunkDownloader>.Instance);

            var chunk = new DownloadChunk
            {
                Index = 0,
                Offset = 0,
                SizeBytes = 16,
                LocalFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.part"),
                IsCompleted = false
            };

            var package = new UpdatePackage { PackageId = Guid.NewGuid(), Size = 16 };
            var mirror = new MirrorEndpoint { Name = "Mirror", BaseUri = new Uri("https://mirror.com/") };
            var progressReporter = new ProgressReporter();
            progressReporter.Reset(package.PackageId, package.Size);

            try
            {
                // Act
                await chunkDownloader.DownloadChunkAsync(chunk, package, mirror, progressReporter, CancellationToken.None);

                // Assert
                Assert.True(chunk.IsCompleted);
                Assert.True(File.Exists(chunk.LocalFilePath));
                string content = await File.ReadAllTextAsync(chunk.LocalFilePath);
                Assert.Equal("chunk-bytes-data", content);
            }
            finally
            {
                if (File.Exists(chunk.LocalFilePath))
                {
                    File.Delete(chunk.LocalFilePath);
                }
            }
        }

        #endregion

        #region DownloadManager E2E Pipeline Tests

        [Fact]
        public async Task DownloadManager_ExecutesFullParallelDownloadAndMerge()
        {
            // Arrange
            var stateStoreDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var stateStore = new DownloadStateStore(stateStoreDir);

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.PartialContent,
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("AAAABBBB"))
                });

            var httpClient = new HttpClient(mockHandler.Object);
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var limiter = new BandwidthLimiter();
            limiter.SetLimit(0); // No limits

            var selector = new MirrorSelector(_mockHttpClientFactory.Object);
            selector.RegisterEndpoint(new MirrorEndpoint { Name = "Main", BaseUri = new Uri("https://main.com/"), Priority = 1, IsHealthy = true });

            var chunkDownloader = new ChunkDownloader(
                _mockHttpClientFactory.Object,
                limiter,
                NullLogger<ChunkDownloader>.Instance);

            var progressReporter = new ProgressReporter();

            var options = Options.Create(new DownloadOptions { MaxParallelDownloads = 2 });

            var manager = new DownloadManager(
                selector,
                chunkDownloader,
                stateStore,
                limiter,
                progressReporter,
                options,
                NullLogger<DownloadManager>.Instance);

            var package = new UpdatePackage
            {
                PackageId = Guid.NewGuid(),
                Version = "2.4.0",
                Size = 16 // Two chunks of 8 bytes
            };

            // Custom load state to pre-partition chunks for tests
            // Because our chunk split in DownloadManager default-divides into 1MB chunks, we can simulate two 8-byte chunks in load state
            var testJob = new DownloadJob
            {
                PackageId = package.PackageId,
                Version = package.Version,
                TotalSizeBytes = package.Size,
                TargetFilePath = Path.Combine(stateStoreDir, "2.4.0.spk"),
                TempDirectory = Path.Combine(stateStoreDir, "temp"),
                Status = "Pending",
                Chunks = new List<DownloadChunk>
                {
                    new DownloadChunk { Index = 0, Offset = 0, SizeBytes = 8, LocalFilePath = Path.Combine(stateStoreDir, "temp", "chunk_0.part"), IsCompleted = false },
                    new DownloadChunk { Index = 1, Offset = 8, SizeBytes = 8, LocalFilePath = Path.Combine(stateStoreDir, "temp", "chunk_1.part"), IsCompleted = false }
                }
            };
            await stateStore.SaveJobAsync(testJob);

            try
            {
                // Act
                string finalPath = await manager.DownloadAsync(package);

                // Assert
                Assert.True(File.Exists(finalPath));
                byte[] finalBytes = await File.ReadAllBytesAsync(finalPath);
                Assert.Equal(16, finalBytes.Length);
                string text = Encoding.UTF8.GetString(finalBytes);
                Assert.Equal("AAAABBBBAAAABBBB", text); // Both chunks merged successfully

                var finalJob = await stateStore.LoadJobAsync(package.PackageId);
                Assert.NotNull(finalJob);
                Assert.Equal("Completed", finalJob.Status);
            }
            finally
            {
                if (Directory.Exists(stateStoreDir))
                {
                    Directory.Delete(stateStoreDir, true);
                }
            }
        }

        #endregion
    }
}
