using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Interfaces;
using Sayra.Client.Shared.GameDistribution.Cache.Interfaces;
using Sayra.Client.Shared.GameDistribution.Transfer.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.GameDistribution.Services
{
    public class DistributedDownloadManager : IDownloadManager
    {
        private readonly IDownloadManager _innerDownloadManager;
        private readonly IDistributedCacheManager _cacheManager;
        private readonly IPeerTransferService _transferService;
        private readonly IBlockStorageService _storageService;
        private readonly IDownloadStateStore _stateStore;
        private readonly ILogger<DistributedDownloadManager> _logger;

        public event EventHandler<DownloadProgress>? ProgressChanged;

        // Custom metrics for Stage 11 tracking
        public static long TotalWanBytesSaved { get; private set; }
        public static long TotalInternetBytesDownloaded { get; private set; }

        public DistributedDownloadManager(
            IDownloadManager innerDownloadManager,
            IDistributedCacheManager cacheManager,
            IPeerTransferService transferService,
            IBlockStorageService storageService,
            IDownloadStateStore stateStore,
            ILogger<DistributedDownloadManager> logger)
        {
            _innerDownloadManager = innerDownloadManager ?? throw new ArgumentNullException(nameof(innerDownloadManager));
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _transferService = transferService ?? throw new ArgumentNullException(nameof(transferService));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _innerDownloadManager.ProgressChanged += (s, e) => ProgressChanged?.Invoke(this, e);
        }

        public void ConfigureBandwidthPolicy(BandwidthPolicy policy)
        {
            _innerDownloadManager.ConfigureBandwidthPolicy(policy);
        }

        public async Task<string> DownloadAsync(UpdatePackage package, CancellationToken cancellationToken = default)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));

            _logger.LogInformation("Distributed Download Engine initiated for package {PackageId}.", package.PackageId);

            // 1. Check/load or initialize download state to get chunks structure
            var job = await _stateStore.LoadJobAsync(package.PackageId, cancellationToken);
            if (job == null)
            {
                // Let's create a temporary job metadata using similar folder layout to pre-fill chunks
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string tempDir = Path.Combine(appData, "SAYRA_Client", "UpdateCache", package.PackageId.ToString("D"));
                string finalPath = Path.Combine(appData, "SAYRA_Client", "UpdateCache", $"{package.PackageId:D}.spk");

                try
                {
                    string parent = Path.GetDirectoryName(tempDir)!;
                    if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                }
                catch
                {
                    appData = Path.GetTempPath();
                    tempDir = Path.Combine(appData, "SAYRA_Client", "UpdateCache", package.PackageId.ToString("D"));
                    finalPath = Path.Combine(appData, "SAYRA_Client", "UpdateCache", $"{package.PackageId:D}.spk");
                }

                long chunkSize = 1024 * 1024; // 1MB chunks
                long totalBytes = package.Size;
                int chunkCount = (int)Math.Max(1, Math.Ceiling((double)totalBytes / chunkSize));

                var chunks = Enumerable.Range(0, chunkCount).Select(i =>
                {
                    long offset = i * chunkSize;
                    long size = Math.Min(chunkSize, totalBytes - offset);
                    return new DownloadChunk
                    {
                        Index = i,
                        Offset = offset,
                        SizeBytes = size,
                        LocalFilePath = Path.Combine(tempDir, $"chunk_{i}.part"),
                        IsCompleted = false,
                        BytesDownloaded = 0
                    };
                }).ToList();

                job = new DownloadJob
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

                await _stateStore.SaveJobAsync(job, cancellationToken);
            }

            // Ensure directory exists
            if (!Directory.Exists(job.TempDirectory))
            {
                Directory.CreateDirectory(job.TempDirectory);
            }

            long lanBytesFetched = 0;
            long totalSize = job.TotalSizeBytes;

            // 2. Pre-fetch blocks/chunks from LAN cache & peers
            foreach (var chunk in job.Chunks.Where(c => !c.IsCompleted))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check local content block storage
                string blockId = $"{package.PackageId}_{chunk.Index}";
                bool isLocalVerified = await _storageService.VerifyBlockAsync(blockId, cancellationToken);

                if (isLocalVerified)
                {
                    _logger.LogInformation("Block '{BlockId}' found in local Content Block Storage. Skipping CDN.", blockId);
                    byte[] data = await _storageService.GetBlockBytesAsync(blockId, cancellationToken);
                    await File.WriteAllBytesAsync(chunk.LocalFilePath, data, cancellationToken);
                    chunk.IsCompleted = true;
                    chunk.BytesDownloaded = chunk.SizeBytes;
                    lanBytesFetched += chunk.SizeBytes;
                    continue;
                }

                // Check if any online LAN node has this block
                var peers = await _cacheManager.GetNodesWithBlockAsync(blockId, cancellationToken);
                if (peers.Any())
                {
                    var bestPeer = peers.First();
                    try
                    {
                        _logger.LogInformation("Downloading block '{BlockId}' from peer node {NodeId}.", blockId, bestPeer.NodeId);
                        byte[] data = await _transferService.TransferBlockAsync(bestPeer, blockId, cancellationToken);
                        await File.WriteAllBytesAsync(chunk.LocalFilePath, data, cancellationToken);
                        chunk.IsCompleted = true;
                        chunk.BytesDownloaded = chunk.SizeBytes;
                        lanBytesFetched += chunk.SizeBytes;
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch block '{BlockId}' from peer node {NodeId}. Will fallback.", blockId, bestPeer.NodeId);
                    }
                }
            }

            if (lanBytesFetched > 0)
            {
                _logger.LogInformation("LAN Optimization Saved {Saved} / {Total} bytes ({Percent:F1}% WAN Reduction).",
                    lanBytesFetched, totalSize, (double)lanBytesFetched / totalSize * 100);
                TotalWanBytesSaved += lanBytesFetched;

                // Save updated job status before passing to inner downloader
                await _stateStore.SaveJobAsync(job, cancellationToken);
            }

            long startingBytes = job.Chunks.Where(c => c.IsCompleted).Sum(c => c.SizeBytes);

            // 3. Delegate to Inner Download Manager to fetch remainder from Internet CDN
            string resultPath = await _innerDownloadManager.DownloadAsync(package, cancellationToken);

            long finalBytes = job.Chunks.Where(c => c.IsCompleted).Sum(c => c.SizeBytes);
            long internetDownloaded = Math.Max(0, finalBytes - startingBytes);
            TotalInternetBytesDownloaded += internetDownloaded;

            return resultPath;
        }
    }
}
