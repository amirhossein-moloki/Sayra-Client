using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class ImpressionTracker : IImpressionTracker
    {
        private readonly IAdvertisementRepository _repository;
        private readonly ILogger<ImpressionTracker> _logger;

        public ImpressionTracker(IAdvertisementRepository repository, ILogger<ImpressionTracker> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task TrackImpressionAsync(
            string campaignId,
            string? sessionId,
            ImpressionType type,
            double durationSeconds,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(campaignId)) throw new ArgumentException("Campaign ID cannot be empty", nameof(campaignId));

            _logger.LogInformation("Tracking impression for campaign '{CampaignId}': Type={Type}, Duration={Duration}s", campaignId, type, durationSeconds);

            var impression = new AdImpression
            {
                ImpressionId = Guid.NewGuid().ToString(),
                CampaignId = campaignId,
                SessionId = sessionId,
                ImpressionType = type,
                PlaybackDurationSeconds = durationSeconds,
                CreatedAt = DateTime.UtcNow,
                IsSynced = false
            };

            await _repository.SaveImpressionAsync(impression, cancellationToken);
        }
    }
}
