using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class AdvertisementCache : IAdvertisementCache
    {
        private readonly IAdvertisementRepository _repository;
        private readonly ILogger<AdvertisementCache> _logger;
        private readonly IAuditLogger _auditLogger;
        private long _cacheQuotaLimitBytes = 500 * 1024 * 1024; // Default 500MB
        private readonly SemaphoreSlim _lock = new(1, 1);

        public AdvertisementCache(
            IAdvertisementRepository repository,
            IAuditLogger auditLogger,
            ILogger<AdvertisementCache> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ConfigureQuotaAsync(long bytesLimit)
        {
            await _lock.WaitAsync();
            try
            {
                _cacheQuotaLimitBytes = bytesLimit;
                _logger.LogInformation("AdvertisementCache quota configured to {LimitMB} MB", bytesLimit / (1024 * 1024));
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<long> GetCacheSizeAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                var list = await _repository.GetDownloadedMediaListAsync(cancellationToken);
                return list.Sum(m => m.FileSize);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<string?> GetCachedMediaPathAsync(AdCampaign campaign, CancellationToken cancellationToken = default)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                var localPath = campaign.MediaLocalPath;
                if (string.IsNullOrEmpty(localPath))
                {
                    localPath = Path.Combine(GetCacheDirectory(), $"{campaign.CampaignId}_{Path.GetFileName(campaign.MediaUrl)}");
                }

                if (File.Exists(localPath))
                {
                    // Register access and update DB
                    await _repository.UpdateDownloadedMediaAccessTimeAsync(campaign.CampaignId, DateTime.UtcNow, cancellationToken);

                    // Update entry
                    var entry = new DownloadedMedia
                    {
                        MediaPath = localPath,
                        CampaignId = campaign.CampaignId,
                        FileSize = new FileInfo(localPath).Length,
                        LastAccessedAt = DateTime.UtcNow,
                        Checksum = campaign.Checksum
                    };
                    await _repository.SaveDownloadedMediaAsync(entry, cancellationToken);

                    return localPath;
                }

                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task RegisterAccessAsync(string campaignId, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                await _repository.UpdateDownloadedMediaAccessTimeAsync(campaignId, DateTime.UtcNow, cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task EvictLeastRecentlyUsedAsync(long requiredBytes, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("EvictLeastRecentlyUsedAsync invoked to free up {RequiredBytes} bytes", requiredBytes);

                var mediaList = await _repository.GetDownloadedMediaListAsync(cancellationToken);
                long currentSize = mediaList.Sum(m => m.FileSize);

                if (currentSize + requiredBytes <= _cacheQuotaLimitBytes)
                {
                    _logger.LogInformation("Current cache size ({CurrentMB} MB) is well within quota limit ({LimitMB} MB). No eviction needed.", currentSize / (1024 * 1024), _cacheQuotaLimitBytes / (1024 * 1024));
                    return;
                }

                // Evict until we have enough space
                var orderedMedia = mediaList.OrderBy(m => m.LastAccessedAt).ToList();
                long bytesEvicted = 0;

                foreach (var media in orderedMedia)
                {
                    if (currentSize - bytesEvicted + requiredBytes <= _cacheQuotaLimitBytes)
                    {
                        break;
                    }

                    _logger.LogInformation("Evicting campaign media '{CampaignId}' ({SizeKB} KB) to free up space", media.CampaignId, media.FileSize / 1024);

                    try
                    {
                        if (File.Exists(media.MediaPath))
                        {
                            File.Delete(media.MediaPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file '{Path}' during LRU eviction", media.MediaPath);
                    }

                    await _repository.DeleteDownloadedMediaAsync(media.CampaignId, cancellationToken);
                    bytesEvicted += media.FileSize;

                    // Audit log eviction
                    _auditLogger.LogSecurity($"Evicted campaign media '{media.CampaignId}' to satisfy cache quota limits.");
                }

                _logger.LogInformation("LRU cache eviction completed. Evicted {Bytes} bytes.", bytesEvicted);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ClearExpiredCacheAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Checking and clearing expired advertisement cache...");
                var campaigns = await _repository.LoadCampaignsAsync(cancellationToken);
                var expiredCampaignIds = campaigns
                    .Where(c => c.EndTime < DateTime.UtcNow)
                    .Select(c => c.CampaignId)
                    .ToHashSet();

                var mediaList = await _repository.GetDownloadedMediaListAsync(cancellationToken);
                foreach (var media in mediaList)
                {
                    if (expiredCampaignIds.Contains(media.CampaignId))
                    {
                        _logger.LogInformation("Evicting expired campaign media '{CampaignId}'", media.CampaignId);
                        try
                        {
                            if (File.Exists(media.MediaPath))
                            {
                                File.Delete(media.MediaPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete expired file '{Path}'", media.MediaPath);
                        }

                        await _repository.DeleteDownloadedMediaAsync(media.CampaignId, cancellationToken);

                        // Log audit
                        _auditLogger.LogSecurity($"Expired campaign media '{media.CampaignId}' was cleaned up from cache.");
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private string GetCacheDirectory()
        {
            var basePath = AppContext.BaseDirectory;
            if (OperatingSystem.IsWindows())
            {
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Sayra", "AdCache");
            }
            else
            {
                basePath = Path.Combine(basePath, "AdCache");
            }

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }
            return basePath;
        }
    }
}
