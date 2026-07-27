using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class CampaignScheduler : ICampaignScheduler
    {
        public bool IsCampaignActiveAtTime(AdCampaign campaign, DateTime timeUtc)
        {
            if (campaign == null) return false;

            // Start and End time checks
            if (timeUtc < campaign.StartTime || timeUtc > campaign.EndTime)
            {
                return false;
            }

            // Check DailyActiveHours JSON
            if (string.IsNullOrEmpty(campaign.DailyActiveHours) || campaign.DailyActiveHours == "[]" || campaign.DailyActiveHours == "null")
            {
                return true; // No daily restriction
            }

            try
            {
                var ranges = JsonSerializer.Deserialize<List<string>>(campaign.DailyActiveHours);
                if (ranges == null || ranges.Count == 0)
                {
                    return true;
                }

                var timeOfDay = timeUtc.TimeOfDay;

                foreach (var range in ranges)
                {
                    if (string.IsNullOrEmpty(range)) continue;
                    var parts = range.Split('-');
                    if (parts.Length != 2) continue;

                    if (TimeSpan.TryParse(parts[0], out var startSpan) && TimeSpan.TryParse(parts[1], out var endSpan))
                    {
                        if (startSpan <= endSpan)
                        {
                            if (timeOfDay >= startSpan && timeOfDay <= endSpan)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            // Handles overnight schedules e.g., 22:00-02:00
                            if (timeOfDay >= startSpan || timeOfDay <= endSpan)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false; // Time of day did not match any range
            }
            catch
            {
                return true; // Fallback to true if parsing fails to avoid blocking the ad
            }
        }

        public Task<AdCampaign?> GetNextPlayableCampaignAsync(List<AdCampaign> campaigns, DateTime currentUtc)
        {
            if (campaigns == null || campaigns.Count == 0)
            {
                return Task.FromResult<AdCampaign?>(null);
            }

            // Filter campaigns that are active, downloaded, and not expired
            var activeCampaigns = campaigns
                .Where(c => IsCampaignActiveAtTime(c, currentUtc) && c.IsDownloaded)
                .OrderByDescending(c => (int)c.Priority)
                .ThenBy(c => c.EndTime) // Resolve conflict: nearest EndTime plays first
                .ThenBy(c => c.CampaignId) // Alphabetical final fallback
                .ToList();

            return Task.FromResult(activeCampaigns.FirstOrDefault());
        }

        public AdCampaign GetFallbackCampaign()
        {
            return new AdCampaign
            {
                CampaignId = "FALLBACK_CAMPAIGN",
                Name = "SAYRA Default Logo",
                Type = CampaignType.IMAGE,
                MediaUrl = "http://localhost/fallback.png",
                MediaLocalPath = "Assets/fallback_emblem.png",
                TargetUrl = "https://sayra.io",
                Priority = CampaignPriority.Low,
                DisplayDurationSeconds = 10,
                StartTime = DateTime.MinValue,
                EndTime = DateTime.MaxValue,
                DailyActiveHours = "[]",
                IsDownloaded = true,
                Checksum = "fallback_checksum",
                Signature = "VALID_TEST_SIGNATURE",
                VersionCode = 1
            };
        }
    }
}
