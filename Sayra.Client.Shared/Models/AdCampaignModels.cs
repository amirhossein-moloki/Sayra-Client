using System;

namespace Sayra.Client.Shared.Models
{
    public enum CampaignPriority
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Emergency = 4
    }

    public enum CampaignType
    {
        IMAGE,
        VIDEO,
        HTML,
        INTERACTIVE
    }

    public class AdCampaign
    {
        public string CampaignId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public CampaignType Type { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string MediaLocalPath { get; set; } = string.Empty;
        public string TargetUrl { get; set; } = string.Empty;
        public CampaignPriority Priority { get; set; } = CampaignPriority.Medium;
        public int DisplayDurationSeconds { get; set; } = 10;

        // Schedule timestamps
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // Daily hours constraint (JSON array, e.g., ["14:00-18:00"])
        public string DailyActiveHours { get; set; } = "[]";

        public bool IsDownloaded { get; set; }
        public string Checksum { get; set; } = string.Empty; // SHA-256 hash expected
        public string Signature { get; set; } = string.Empty; // RSA Signature of campaign payload
        public long MediaSize { get; set; }
        public int VersionCode { get; set; } = 1;
    }

    public class DownloadedMedia
    {
        public string MediaPath { get; set; } = string.Empty;
        public string CampaignId { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public string Checksum { get; set; } = string.Empty;
    }

    public enum ImpressionType
    {
        VIEW,
        CLICK,
        SKIP
    }

    public class AdImpression
    {
        public string ImpressionId { get; set; } = string.Empty;
        public string CampaignId { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public ImpressionType ImpressionType { get; set; }
        public double PlaybackDurationSeconds { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsSynced { get; set; }
    }

    public class PlaybackHistoryEntry
    {
        public string PlaybackId { get; set; } = string.Empty;
        public string CampaignId { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public double DurationSeconds { get; set; }
        public string Status { get; set; } = string.Empty; // COMPLETED, FAILED, TIMEOUT
        public string? ErrorMessage { get; set; }
    }
}
