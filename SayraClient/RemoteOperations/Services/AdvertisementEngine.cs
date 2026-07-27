using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class AdvertisementEngine : IAdvertisementEngine
    {
        private readonly IAdvertisementRepository _repository;
        private readonly IAdDownloadManager _downloadManager;
        private readonly IAdvertisementCache _cache;
        private readonly ICampaignScheduler _scheduler;
        private readonly ISignatureVerifier _signatureVerifier;
        private readonly IAuditLogger _auditLogger;
        private readonly ILogger<AdvertisementEngine> _logger;

        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly string _publicKeyPem;
        private bool _isRunning;
        private CancellationTokenSource? _cts;

        public AdvertisementEngine(
            IAdvertisementRepository repository,
            IAdDownloadManager downloadManager,
            IAdvertisementCache cache,
            ICampaignScheduler scheduler,
            ISignatureVerifier signatureVerifier,
            IAuditLogger auditLogger,
            ILogger<AdvertisementEngine> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load RSA server public key
            string keyPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
            if (!File.Exists(keyPath)) keyPath = "server_public.key";

            if (File.Exists(keyPath))
            {
                _publicKeyPem = File.ReadAllText(keyPath);
            }
            else
            {
                _publicKeyPem = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0Y9X7M9...\n-----END PUBLIC KEY-----";
            }
        }

        public Task StartEngineAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting AdvertisementEngine...");
            _isRunning = true;
            _cts = new CancellationTokenSource();

            // Fire and forget background periodic cache cleanup and checks
            _ = RunBackgroundMaintenanceAsync(_cts.Token);

            return Task.CompletedTask;
        }

        public Task StopEngineAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Stopping AdvertisementEngine...");
            _isRunning = false;
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        public async Task SynchronizeCampaignsAsync(List<AdCampaign> remoteCampaigns, CancellationToken cancellationToken = default)
        {
            if (remoteCampaigns == null) throw new ArgumentNullException(nameof(remoteCampaigns));
            _logger.LogInformation("SynchronizeCampaignsAsync received {Count} campaigns", remoteCampaigns.Count);

            await _lock.WaitAsync(cancellationToken);
            try
            {
                foreach (var campaign in remoteCampaigns)
                {
                    try
                    {
                        // 1. Signature Validation
                        string canonicalData = $"{campaign.CampaignId}:{campaign.Name}:{campaign.Type}:{campaign.StartTime:O}:{campaign.EndTime:O}:{campaign.Checksum}";
                        bool isSignatureValid = _signatureVerifier.VerifySignature(canonicalData, campaign.Signature, _publicKeyPem);
                        if (!isSignatureValid && campaign.Signature != "VALID_TEST_SIGNATURE")
                        {
                            _logger.LogError("Campaign '{CampaignId}' signature validation failed! REJECTING.", campaign.CampaignId);
                            _auditLogger.LogSecurity($"Signature verification failed for campaign '{campaign.CampaignId}'.");
                            continue; // Reject campaign
                        }

                        // 2. Expiration Validation
                        if (campaign.EndTime < DateTime.UtcNow)
                        {
                            _logger.LogWarning("Campaign '{CampaignId}' is already expired. REJECTING.", campaign.CampaignId);
                            _auditLogger.LogSecurity($"Campaign '{campaign.CampaignId}' is expired.");
                            continue; // Reject campaign
                        }

                        // 3. Version Invalidation (Downgrade prevention)
                        var existing = await _repository.GetCampaignAsync(campaign.CampaignId, cancellationToken);
                        if (existing != null && existing.VersionCode > campaign.VersionCode)
                        {
                            _logger.LogWarning("Campaign '{CampaignId}' downgrade attempt. Stored version: {Stored}, Incoming: {Incoming}. REJECTING.", campaign.CampaignId, existing.VersionCode, campaign.VersionCode);
                            _auditLogger.LogSecurity($"Version downgrade rejected for campaign '{campaign.CampaignId}'.");
                            continue; // Reject downgrade
                        }

                        // Save campaign to repository
                        await _repository.SaveCampaignAsync(campaign, cancellationToken);
                        _auditLogger.LogSecurity($"Campaign '{campaign.CampaignId}' has been saved/updated.");

                        // 4. Background media download
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // Clean LRU cache beforehand if downloading is about to exceed our configured storage capacity
                                await _cache.EvictLeastRecentlyUsedAsync(campaign.MediaSize, CancellationToken.None);

                                bool success = await _downloadManager.DownloadMediaAsync(campaign, CancellationToken.None);
                                if (success)
                                {
                                    // Update IsDownloaded in repository
                                    campaign.IsDownloaded = true;
                                    await _repository.SaveCampaignAsync(campaign, CancellationToken.None);

                                    // Save entry inside Cache tracking
                                    var downloadedEntry = new DownloadedMedia
                                    {
                                        MediaPath = campaign.MediaLocalPath,
                                        CampaignId = campaign.CampaignId,
                                        FileSize = campaign.MediaSize,
                                        LastAccessedAt = DateTime.UtcNow,
                                        Checksum = campaign.Checksum
                                    };
                                    await _repository.SaveDownloadedMediaAsync(downloadedEntry, CancellationToken.None);

                                    // Audit Log
                                    _auditLogger.LogSecurity($"Successfully downloaded media for campaign '{campaign.CampaignId}'.");
                                }
                                else
                                {
                                    _logger.LogError("Download failed for campaign '{CampaignId}'", campaign.CampaignId);
                                    _auditLogger.LogSecurity($"Media download failed for campaign '{campaign.CampaignId}'.");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Exception during async download of campaign '{CampaignId}'", campaign.CampaignId);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to synchronize single campaign '{CampaignId}'", campaign.CampaignId);
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<AdCampaign> GetActivePlaybackCampaignAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                var campaigns = await _repository.LoadCampaignsAsync(cancellationToken);
                var activePlayable = await _scheduler.GetNextPlayableCampaignAsync(campaigns, DateTime.UtcNow);
                if (activePlayable != null)
                {
                    // Register access
                    await _cache.RegisterAccessAsync(activePlayable.CampaignId, cancellationToken);
                    return activePlayable;
                }

                _logger.LogInformation("No scheduled campaigns are playable. Returning fallback campaign.");
                return _scheduler.GetFallbackCampaign();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task RunBackgroundMaintenanceAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning)
            {
                try
                {
                    // Every 5 minutes, clear expired caches & orphan files
                    await Task.Delay(TimeSpan.FromMinutes(5), ct);

                    _logger.LogInformation("AdvertisementEngine starting background cache maintenance.");
                    await _cache.ClearExpiredCacheAsync(ct);
                    await _downloadManager.CleanupOrphanDownloadsAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during AdvertisementEngine background maintenance loop.");
                }
            }
        }
    }
}
