using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class AdvertisementRepository : IAdvertisementRepository
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly ILogger<AdvertisementRepository> _logger;

        public AdvertisementRepository(
            ILocalDatabaseService databaseService,
            ILogger<AdvertisementRepository> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private static DbParameter CreateParam(DbCommand cmd, string name, object? value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            return param;
        }

        #region Campaigns

        public async Task SaveCampaignAsync(AdCampaign campaign, CancellationToken ct = default)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            _logger.LogInformation("Saving campaign '{CampaignId}' ({Name})", campaign.CampaignId, campaign.Name);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO AdCampaigns (
                        CampaignId, Name, Type, MediaUrl, MediaLocalPath, TargetUrl, Priority,
                        DisplayDurationSeconds, StartTime, EndTime, DailyActiveHours, IsDownloaded, Checksum, Signature, MediaSize, VersionCode
                    ) VALUES (
                        $campaignId, $name, $type, $mediaUrl, $mediaLocalPath, $targetUrl, $priority,
                        $duration, $startTime, $endTime, $dailyActiveHours, $isDownloaded, $checksum, $signature, $mediaSize, $versionCode
                    );";

                cmd.Parameters.Add(CreateParam(cmd, "$campaignId", campaign.CampaignId));
                cmd.Parameters.Add(CreateParam(cmd, "$name", campaign.Name));
                cmd.Parameters.Add(CreateParam(cmd, "$type", campaign.Type.ToString()));
                cmd.Parameters.Add(CreateParam(cmd, "$mediaUrl", campaign.MediaUrl));
                cmd.Parameters.Add(CreateParam(cmd, "$mediaLocalPath", campaign.MediaLocalPath));
                cmd.Parameters.Add(CreateParam(cmd, "$targetUrl", campaign.TargetUrl));
                cmd.Parameters.Add(CreateParam(cmd, "$priority", (int)campaign.Priority));
                cmd.Parameters.Add(CreateParam(cmd, "$duration", campaign.DisplayDurationSeconds));
                cmd.Parameters.Add(CreateParam(cmd, "$startTime", campaign.StartTime.ToString("O")));
                cmd.Parameters.Add(CreateParam(cmd, "$endTime", campaign.EndTime.ToString("O")));
                cmd.Parameters.Add(CreateParam(cmd, "$dailyActiveHours", campaign.DailyActiveHours));
                cmd.Parameters.Add(CreateParam(cmd, "$isDownloaded", campaign.IsDownloaded ? 1 : 0));
                cmd.Parameters.Add(CreateParam(cmd, "$checksum", campaign.Checksum));
                cmd.Parameters.Add(CreateParam(cmd, "$signature", campaign.Signature));
                cmd.Parameters.Add(CreateParam(cmd, "$mediaSize", campaign.MediaSize));
                cmd.Parameters.Add(CreateParam(cmd, "$versionCode", campaign.VersionCode));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to save campaign '{CampaignId}'.", campaign.CampaignId);
                throw;
            }
        }

        public async Task<AdCampaign?> GetCampaignAsync(string campaignId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(campaignId)) throw new ArgumentException("Campaign ID cannot be empty", nameof(campaignId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT CampaignId, Name, Type, MediaUrl, MediaLocalPath, TargetUrl, Priority,
                       DisplayDurationSeconds, StartTime, EndTime, DailyActiveHours, IsDownloaded, Checksum, Signature, MediaSize, VersionCode
                FROM AdCampaigns WHERE CampaignId = $campaignId;";
            cmd.Parameters.Add(CreateParam(cmd, "$campaignId", campaignId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return MapReaderToCampaign(reader);
            }

            return null;
        }

        public async Task<List<AdCampaign>> LoadCampaignsAsync(CancellationToken ct = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT CampaignId, Name, Type, MediaUrl, MediaLocalPath, TargetUrl, Priority,
                       DisplayDurationSeconds, StartTime, EndTime, DailyActiveHours, IsDownloaded, Checksum, Signature, MediaSize, VersionCode
                FROM AdCampaigns;";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<AdCampaign>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(MapReaderToCampaign(reader));
            }

            return list;
        }

        public async Task<List<AdCampaign>> GetActiveCampaignsAsync(CancellationToken ct = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            var nowStr = DateTime.UtcNow.ToString("O");

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT CampaignId, Name, Type, MediaUrl, MediaLocalPath, TargetUrl, Priority,
                       DisplayDurationSeconds, StartTime, EndTime, DailyActiveHours, IsDownloaded, Checksum, Signature, MediaSize, VersionCode
                FROM AdCampaigns
                WHERE StartTime <= $now AND EndTime >= $now;";
            cmd.Parameters.Add(CreateParam(cmd, "$now", nowStr));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<AdCampaign>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(MapReaderToCampaign(reader));
            }

            return list;
        }

        public async Task UpdateCampaignAsync(AdCampaign campaign, CancellationToken ct = default)
        {
            await SaveCampaignAsync(campaign, ct);
        }

        public async Task DeleteCampaignAsync(string campaignId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(campaignId)) throw new ArgumentException("Campaign ID cannot be empty", nameof(campaignId));
            _logger.LogInformation("Deleting campaign '{CampaignId}'", campaignId);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM AdCampaigns WHERE CampaignId = $campaignId;";
                cmd.Parameters.Add(CreateParam(cmd, "$campaignId", campaignId));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to delete campaign '{CampaignId}'.", campaignId);
                throw;
            }
        }

        private static AdCampaign MapReaderToCampaign(DbDataReader reader)
        {
            return new AdCampaign
            {
                CampaignId = reader.GetString(0),
                Name = reader.GetString(1),
                Type = Enum.Parse<CampaignType>(reader.GetString(2), true),
                MediaUrl = reader.GetString(3),
                MediaLocalPath = reader.GetString(4),
                TargetUrl = reader.GetString(5),
                Priority = (CampaignPriority)reader.GetInt32(6),
                DisplayDurationSeconds = reader.GetInt32(7),
                StartTime = DateTime.Parse(reader.GetString(8)),
                EndTime = DateTime.Parse(reader.GetString(9)),
                DailyActiveHours = reader.GetString(10),
                IsDownloaded = reader.GetInt32(11) == 1,
                Checksum = reader.GetString(12),
                Signature = reader.GetString(13),
                MediaSize = reader.GetInt64(14),
                VersionCode = reader.GetInt32(15)
            };
        }

        #endregion

        #region Downloaded Media Tracking

        public async Task SaveDownloadedMediaAsync(DownloadedMedia media, CancellationToken ct = default)
        {
            if (media == null) throw new ArgumentNullException(nameof(media));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO DownloadedMedia (MediaPath, CampaignId, FileSize, LastAccessedAt, Checksum)
                    VALUES ($mediaPath, $campaignId, $fileSize, $lastAccessed, $checksum);";

                cmd.Parameters.Add(CreateParam(cmd, "$mediaPath", media.MediaPath));
                cmd.Parameters.Add(CreateParam(cmd, "$campaignId", media.CampaignId));
                cmd.Parameters.Add(CreateParam(cmd, "$fileSize", media.FileSize));
                cmd.Parameters.Add(CreateParam(cmd, "$lastAccessed", media.LastAccessedAt.ToString("O")));
                cmd.Parameters.Add(CreateParam(cmd, "$checksum", media.Checksum));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to save downloaded media entry for campaign '{CampaignId}'.", media.CampaignId);
                throw;
            }
        }

        public async Task<List<DownloadedMedia>> GetDownloadedMediaListAsync(CancellationToken ct = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT MediaPath, CampaignId, FileSize, LastAccessedAt, Checksum FROM DownloadedMedia;";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<DownloadedMedia>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(new DownloadedMedia
                {
                    MediaPath = reader.GetString(0),
                    CampaignId = reader.GetString(1),
                    FileSize = reader.GetInt64(2),
                    LastAccessedAt = DateTime.Parse(reader.GetString(3)),
                    Checksum = reader.GetString(4)
                });
            }

            return list;
        }

        public async Task DeleteDownloadedMediaAsync(string campaignId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(campaignId)) throw new ArgumentException("Campaign ID cannot be empty", nameof(campaignId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM DownloadedMedia WHERE CampaignId = $campaignId;";
                cmd.Parameters.Add(CreateParam(cmd, "$campaignId", campaignId));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to delete downloaded media entry for campaign '{CampaignId}'.", campaignId);
                throw;
            }
        }

        public async Task UpdateDownloadedMediaAccessTimeAsync(string campaignId, DateTime lastAccessed, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(campaignId)) throw new ArgumentException("Campaign ID cannot be empty", nameof(campaignId));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "UPDATE DownloadedMedia SET LastAccessedAt = $lastAccessed WHERE CampaignId = $campaignId;";
                cmd.Parameters.Add(CreateParam(cmd, "$campaignId", campaignId));
                cmd.Parameters.Add(CreateParam(cmd, "$lastAccessed", lastAccessed.ToString("O")));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to update last access time for campaign '{CampaignId}'.", campaignId);
                throw;
            }
        }

        #endregion

        #region Impressions

        public async Task SaveImpressionAsync(AdImpression impression, CancellationToken ct = default)
        {
            if (impression == null) throw new ArgumentNullException(nameof(impression));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO AdImpressions (ImpressionId, CampaignId, SessionId, ImpressionType, PlaybackDurationSeconds, CreatedAt, IsSynced)
                    VALUES ($impId, $campaignId, $sessionId, $type, $duration, $createdAt, $isSynced);";

                cmd.Parameters.Add(CreateParam(cmd, "$impId", impression.ImpressionId));
                cmd.Parameters.Add(CreateParam(cmd, "$campaignId", impression.CampaignId));
                cmd.Parameters.Add(CreateParam(cmd, "$sessionId", impression.SessionId));
                cmd.Parameters.Add(CreateParam(cmd, "$type", impression.ImpressionType.ToString()));
                cmd.Parameters.Add(CreateParam(cmd, "$duration", impression.PlaybackDurationSeconds));
                cmd.Parameters.Add(CreateParam(cmd, "$createdAt", impression.CreatedAt.ToString("O")));
                cmd.Parameters.Add(CreateParam(cmd, "$isSynced", impression.IsSynced ? 1 : 0));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to save impression '{ImpressionId}'.", impression.ImpressionId);
                throw;
            }
        }

        public async Task<List<AdImpression>> GetUnsyncedImpressionsAsync(CancellationToken ct = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT ImpressionId, CampaignId, SessionId, ImpressionType, PlaybackDurationSeconds, CreatedAt, IsSynced
                FROM AdImpressions WHERE IsSynced = 0;";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<AdImpression>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(new AdImpression
                {
                    ImpressionId = reader.GetString(0),
                    CampaignId = reader.GetString(1),
                    SessionId = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ImpressionType = Enum.Parse<ImpressionType>(reader.GetString(3), true),
                    PlaybackDurationSeconds = reader.GetDouble(4),
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    IsSynced = reader.GetInt32(6) == 1
                });
            }

            return list;
        }

        public async Task MarkImpressionsAsSyncedAsync(List<string> impressionIds, CancellationToken ct = default)
        {
            if (impressionIds == null || impressionIds.Count == 0) return;

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                foreach (var id in impressionIds)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = "UPDATE AdImpressions SET IsSynced = 1 WHERE ImpressionId = $id;";
                    cmd.Parameters.Add(CreateParam(cmd, "$id", id));
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to mark impressions as synced.");
                throw;
            }
        }

        #endregion

        #region Playback History

        public async Task SavePlaybackHistoryAsync(PlaybackHistoryEntry entry, CancellationToken ct = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO PlaybackHistory (PlaybackId, CampaignId, StartedAt, CompletedAt, DurationSeconds, Status, ErrorMessage)
                    VALUES ($pbId, $campaignId, $started, $completed, $duration, $status, $error);";

                cmd.Parameters.Add(CreateParam(cmd, "$pbId", entry.PlaybackId));
                cmd.Parameters.Add(CreateParam(cmd, "$campaignId", entry.CampaignId));
                cmd.Parameters.Add(CreateParam(cmd, "$started", entry.StartedAt.ToString("O")));
                cmd.Parameters.Add(CreateParam(cmd, "$completed", entry.CompletedAt.ToString("O")));
                cmd.Parameters.Add(CreateParam(cmd, "$duration", entry.DurationSeconds));
                cmd.Parameters.Add(CreateParam(cmd, "$status", entry.Status));
                cmd.Parameters.Add(CreateParam(cmd, "$error", entry.ErrorMessage));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to save playback history entry '{PlaybackId}'.", entry.PlaybackId);
                throw;
            }
        }

        public async Task<List<PlaybackHistoryEntry>> GetPlaybackHistoryAsync(CancellationToken ct = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT PlaybackId, CampaignId, StartedAt, CompletedAt, DurationSeconds, Status, ErrorMessage FROM PlaybackHistory;";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<PlaybackHistoryEntry>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(new PlaybackHistoryEntry
                {
                    PlaybackId = reader.GetString(0),
                    CampaignId = reader.GetString(1),
                    StartedAt = DateTime.Parse(reader.GetString(2)),
                    CompletedAt = DateTime.Parse(reader.GetString(3)),
                    DurationSeconds = reader.GetDouble(4),
                    Status = reader.GetString(5),
                    ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            }

            return list;
        }

        #endregion
    }
}
