using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.GameDistribution.BlockStorage.Interfaces;
using Sayra.Client.Shared.GameDistribution.Cache.Interfaces;
using Sayra.Client.Shared.GameDistribution.Optimization.Interfaces;

namespace Sayra.Client.Shared.GameDistribution.Optimization.Services
{
    public class CacheOptimizationService : ICacheOptimizationService
    {
        private readonly IDistributedCacheManager _cacheManager;
        private readonly IBlockStorageService _storageService;
        private readonly IBlockRepository _blockRepository;
        private readonly ILogger<CacheOptimizationService> _logger;

        public CacheOptimizationService(
            IDistributedCacheManager cacheManager,
            IBlockStorageService storageService,
            IBlockRepository blockRepository,
            ILogger<CacheOptimizationService> logger)
        {
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _blockRepository = blockRepository ?? throw new ArgumentNullException(nameof(blockRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task OptimizeCacheAsync(long targetFreeBytes, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Checking distributed local cache optimization needs...");

            // Determine local drive free space
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string drivePath = string.IsNullOrEmpty(appData) ? AppContext.BaseDirectory : appData;
            var driveInfo = new DriveInfo(Path.GetPathRoot(drivePath)!);

            long currentFreeBytes = driveInfo.AvailableFreeSpace;
            _logger.LogInformation("Current drive available space: {CurrentFree} bytes. Target free space: {TargetFree} bytes.",
                currentFreeBytes, targetFreeBytes);

            if (currentFreeBytes >= targetFreeBytes)
            {
                _logger.LogInformation("Sufficient disk space is available. No cache optimization or eviction is needed.");
                return;
            }

            long bytesToEvict = targetFreeBytes - currentFreeBytes;
            _logger.LogWarning("Disk space pressure detected. Attempting to evict {EvictBytes} bytes from local game cache...", bytesToEvict);

            // Fetch game cache entries sorted by LRU (Least Recently Used)
            var gameEntries = await _cacheManager.GetAllGameEntriesAsync(cancellationToken);
            var sortedGames = gameEntries.OrderBy(g => g.LastUsedUtc).ToList();

            long totalEvicted = 0;

            foreach (var game in sortedGames)
            {
                if (totalEvicted >= bytesToEvict) break;

                _logger.LogInformation("Evicting block-level cache for game {GameId} (Last Used: {LastUsed}) to free space...",
                    game.GameId, game.LastUsedUtc);

                var blocks = await _blockRepository.GetByGameAsync(game.GameId, cancellationToken);
                foreach (var block in blocks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await _storageService.DeleteBlockAsync(block.BlockId, cancellationToken);
                        totalEvicted += block.Size;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to evict block '{BlockId}' during cache optimization.", block.BlockId);
                    }

                    if (totalEvicted >= bytesToEvict) break;
                }
            }

            _logger.LogInformation("Cache optimization cycle complete. Evicted {TotalEvicted} bytes of game cache blocks.", totalEvicted);
        }
    }
}
