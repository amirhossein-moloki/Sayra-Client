using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IAdvertisementRepository
    {
        Task SaveCampaignAsync(AdCampaign campaign, CancellationToken cancellationToken = default);
        Task<AdCampaign?> GetCampaignAsync(string campaignId, CancellationToken cancellationToken = default);
        Task<List<AdCampaign>> LoadCampaignsAsync(CancellationToken cancellationToken = default);
        Task<List<AdCampaign>> GetActiveCampaignsAsync(CancellationToken cancellationToken = default);
        Task UpdateCampaignAsync(AdCampaign campaign, CancellationToken cancellationToken = default);
        Task DeleteCampaignAsync(string campaignId, CancellationToken cancellationToken = default);

        // Downloaded media tracking (LRU Cache)
        Task SaveDownloadedMediaAsync(DownloadedMedia media, CancellationToken cancellationToken = default);
        Task<List<DownloadedMedia>> GetDownloadedMediaListAsync(CancellationToken cancellationToken = default);
        Task DeleteDownloadedMediaAsync(string campaignId, CancellationToken cancellationToken = default);
        Task UpdateDownloadedMediaAccessTimeAsync(string campaignId, DateTime lastAccessed, CancellationToken cancellationToken = default);

        // Impression tracking
        Task SaveImpressionAsync(AdImpression impression, CancellationToken cancellationToken = default);
        Task<List<AdImpression>> GetUnsyncedImpressionsAsync(CancellationToken cancellationToken = default);
        Task MarkImpressionsAsSyncedAsync(List<string> impressionIds, CancellationToken cancellationToken = default);

        // Playback history tracking
        Task SavePlaybackHistoryAsync(PlaybackHistoryEntry entry, CancellationToken cancellationToken = default);
        Task<List<PlaybackHistoryEntry>> GetPlaybackHistoryAsync(CancellationToken cancellationToken = default);
    }

    public interface IAdDownloadManager
    {
        Task<bool> DownloadMediaAsync(AdCampaign campaign, CancellationToken cancellationToken = default);
        Task<bool> ResumeDownloadAsync(AdCampaign campaign, string tempPath, CancellationToken cancellationToken = default);
        Task CleanupOrphanDownloadsAsync(CancellationToken cancellationToken = default);
        Task SetDiskQuotaLimitAsync(long bytesLimit);
        Task<long> GetDiskQuotaUsageAsync();
    }

    public interface IAdvertisementCache
    {
        Task<string?> GetCachedMediaPathAsync(AdCampaign campaign, CancellationToken cancellationToken = default);
        Task RegisterAccessAsync(string campaignId, CancellationToken cancellationToken = default);
        Task EvictLeastRecentlyUsedAsync(long requiredBytes, CancellationToken cancellationToken = default);
        Task ClearExpiredCacheAsync(CancellationToken cancellationToken = default);
        Task ConfigureQuotaAsync(long bytesLimit);
        Task<long> GetCacheSizeAsync(CancellationToken cancellationToken = default);
    }

    public interface ICampaignScheduler
    {
        Task<AdCampaign?> GetNextPlayableCampaignAsync(List<AdCampaign> campaigns, DateTime currentUtc);
        AdCampaign GetFallbackCampaign();
        bool IsCampaignActiveAtTime(AdCampaign campaign, DateTime timeUtc);
    }

    public interface IAdvertisementEngine
    {
        Task StartEngineAsync(CancellationToken cancellationToken = default);
        Task StopEngineAsync(CancellationToken cancellationToken = default);
        Task SynchronizeCampaignsAsync(List<AdCampaign> remoteCampaigns, CancellationToken cancellationToken = default);
        Task<AdCampaign> GetActivePlaybackCampaignAsync(CancellationToken cancellationToken = default);
    }

    public interface IMediaPlaybackService
    {
        event Action<AdCampaign>? OnPlaybackStarted;
        event Action<AdCampaign, string?>? OnPlaybackFailed;
        event Action<AdCampaign>? OnPlaybackCompleted;

        Task StartPlaybackAsync(AdCampaign campaign, CancellationToken cancellationToken = default);
        Task StopPlaybackAsync(CancellationToken cancellationToken = default);
    }

    public interface IImpressionTracker
    {
        Task TrackImpressionAsync(string campaignId, string? sessionId, ImpressionType type, double durationSeconds, CancellationToken cancellationToken = default);
    }
}
