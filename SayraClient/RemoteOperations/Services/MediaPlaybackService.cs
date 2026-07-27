using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class MediaPlaybackService : IMediaPlaybackService
    {
        private readonly IAdvertisementRepository _repository;
        private readonly IAuditLogger _auditLogger;
        private readonly ILogger<MediaPlaybackService> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private AdCampaign? _currentCampaign;
        private CancellationTokenSource? _cts;

        public event Action<AdCampaign>? OnPlaybackStarted;
        public event Action<AdCampaign, string?>? OnPlaybackFailed;
        public event Action<AdCampaign>? OnPlaybackCompleted;

        public MediaPlaybackService(
            IAdvertisementRepository repository,
            IAuditLogger auditLogger,
            ILogger<MediaPlaybackService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartPlaybackAsync(AdCampaign campaign, CancellationToken cancellationToken = default)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));

            await _lock.WaitAsync(cancellationToken);
            try
            {
                // Stop current if playing
                if (_currentCampaign != null)
                {
                    _cts?.Cancel();
                }

                _currentCampaign = campaign;
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                _logger.LogInformation("Starting playback for campaign '{CampaignId}'", campaign.CampaignId);

                // Notify listeners
                OnPlaybackStarted?.Invoke(campaign);

                // Save to audit log
                _auditLogger.LogSecurity($"Playback started for campaign '{campaign.CampaignId}' ({campaign.Type}).");

                // Playback monitor (non-blocking)
                _ = MonitorPlaybackAsync(campaign, _cts.Token);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task StopPlaybackAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_currentCampaign != null)
                {
                    _logger.LogInformation("Stopping playback for campaign '{CampaignId}'", _currentCampaign.CampaignId);
                    _cts?.Cancel();
                    _currentCampaign = null;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task MonitorPlaybackAsync(AdCampaign campaign, CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();
            var playbackId = Guid.NewGuid().ToString();

            try
            {
                // Wait for the duration of display, with timeout safety
                int durationMs = campaign.DisplayDurationSeconds * 1000;
                if (durationMs <= 0) durationMs = 10000; // default 10s

                await Task.Delay(durationMs, ct);

                stopwatch.Stop();

                // Save play record
                var historyEntry = new PlaybackHistoryEntry
                {
                    PlaybackId = playbackId,
                    CampaignId = campaign.CampaignId,
                    StartedAt = DateTime.UtcNow.AddSeconds(-stopwatch.Elapsed.TotalSeconds),
                    CompletedAt = DateTime.UtcNow,
                    DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                    Status = "COMPLETED"
                };
                await _repository.SavePlaybackHistoryAsync(historyEntry, CancellationToken.None);

                // Fire completion event
                OnPlaybackCompleted?.Invoke(campaign);

                // Audit log
                _auditLogger.LogSecurity($"Playback completed for campaign '{campaign.CampaignId}' in {stopwatch.Elapsed.TotalSeconds:F1}s.");
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                // Playback was cancelled / skipped
                var historyEntry = new PlaybackHistoryEntry
                {
                    PlaybackId = playbackId,
                    CampaignId = campaign.CampaignId,
                    StartedAt = DateTime.UtcNow.AddSeconds(-stopwatch.Elapsed.TotalSeconds),
                    CompletedAt = DateTime.UtcNow,
                    DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                    Status = "SKIPPED"
                };
                await _repository.SavePlaybackHistoryAsync(historyEntry, CancellationToken.None);

                _logger.LogInformation("Playback cancelled/skipped for campaign '{CampaignId}'", campaign.CampaignId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error during playback of campaign '{CampaignId}'", campaign.CampaignId);

                var historyEntry = new PlaybackHistoryEntry
                {
                    PlaybackId = playbackId,
                    CampaignId = campaign.CampaignId,
                    StartedAt = DateTime.UtcNow.AddSeconds(-stopwatch.Elapsed.TotalSeconds),
                    CompletedAt = DateTime.UtcNow,
                    DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                    Status = "FAILED",
                    ErrorMessage = ex.Message
                };
                await _repository.SavePlaybackHistoryAsync(historyEntry, CancellationToken.None);

                // Notify failure
                OnPlaybackFailed?.Invoke(campaign, ex.Message);

                // Audit log failure
                _auditLogger.LogSecurity($"Playback failed for campaign '{campaign.CampaignId}'. Error: {ex.Message}");
            }
        }
    }
}
