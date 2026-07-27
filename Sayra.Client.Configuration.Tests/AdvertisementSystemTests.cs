using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Models;
using SayraClient.RemoteOperations.Services;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    [Collection("Stage6Tests")]
    public class AdvertisementSystemTests : IDisposable
    {
        private readonly string _testDbDir;
        private readonly string _testDbPath;
        private readonly Mock<ILogger<LocalDatabaseService>> _dbLoggerMock;
        private readonly Mock<ILogger<DatabaseMigrationService>> _migrationLoggerMock;
        private readonly Mock<ILogger<AdvertisementRepository>> _repoLoggerMock;
        private readonly Mock<ILogger<AdDownloadManager>> _downloadLoggerMock;
        private readonly Mock<ILogger<AdvertisementCache>> _cacheLoggerMock;
        private readonly Mock<ILogger<MediaPlaybackService>> _playbackLoggerMock;
        private readonly Mock<ILogger<ImpressionTracker>> _impressionLoggerMock;
        private readonly Mock<ILogger<AdvertisementEngine>> _engineLoggerMock;

        private readonly Mock<IAuditLogger> _auditLoggerMock;
        private readonly Mock<ISignatureVerifier> _sigVerifierMock;

        public AdvertisementSystemTests()
        {
            _testDbDir = Path.Combine(AppContext.BaseDirectory, "Stage6TestData", Guid.NewGuid().ToString());
            if (Directory.Exists(_testDbDir))
            {
                Directory.Delete(_testDbDir, true);
            }
            Directory.CreateDirectory(_testDbDir);
            _testDbPath = Path.Combine(_testDbDir, "remote_commands.db");

            _dbLoggerMock = new Mock<ILogger<LocalDatabaseService>>();
            _migrationLoggerMock = new Mock<ILogger<DatabaseMigrationService>>();
            _repoLoggerMock = new Mock<ILogger<AdvertisementRepository>>();
            _downloadLoggerMock = new Mock<ILogger<AdDownloadManager>>();
            _cacheLoggerMock = new Mock<ILogger<AdvertisementCache>>();
            _playbackLoggerMock = new Mock<ILogger<MediaPlaybackService>>();
            _impressionLoggerMock = new Mock<ILogger<ImpressionTracker>>();
            _engineLoggerMock = new Mock<ILogger<AdvertisementEngine>>();

            _auditLoggerMock = new Mock<IAuditLogger>();
            _sigVerifierMock = new Mock<ISignatureVerifier>();
            _sigVerifierMock.Setup(s => s.VerifySignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                            .Returns(true);

            Environment.SetEnvironmentVariable("SAYRA_TEST_DB_PATH", _testDbPath);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            try
            {
                if (Directory.Exists(_testDbDir))
                {
                    Directory.Delete(_testDbDir, true);
                }
            }
            catch { }

            Environment.SetEnvironmentVariable("SAYRA_TEST_DB_PATH", null);
        }

        private LocalDatabaseService CreateDbService()
        {
            var migrationService = new DatabaseMigrationService(_migrationLoggerMock.Object);
            return new LocalDatabaseService(_dbLoggerMock.Object, migrationService, null);
        }

        private static string ComputeSha256(string content)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        #region Database & Migration Tests

        [Fact]
        public async Task Migration_4_Creates_Advertisement_Tables_And_Indexes()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            Assert.True(File.Exists(_testDbPath));

            using var connection = dbService.CreateConnection();
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM sqlite_master
                WHERE type='table' AND name IN ('AdCampaigns', 'AdImpressions', 'DownloadedMedia', 'PlaybackHistory');";
            var tableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(4, tableCount);
        }

        #endregion

        #region Campaign Repository Tests

        [Fact]
        public async Task Repository_Save_And_Retrieve_Campaign_And_Impressions()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new AdvertisementRepository(dbService, _repoLoggerMock.Object);

            var campaign = new AdCampaign
            {
                CampaignId = "CAM-01",
                Name = "Sponsor Ad",
                Type = CampaignType.IMAGE,
                MediaUrl = "http://example.com/ad.jpg",
                MediaLocalPath = "AdCache/ad.jpg",
                TargetUrl = "http://target.com",
                Priority = CampaignPriority.High,
                DisplayDurationSeconds = 15,
                StartTime = DateTime.UtcNow.AddMinutes(-5),
                EndTime = DateTime.UtcNow.AddMinutes(10),
                DailyActiveHours = "[]",
                IsDownloaded = false,
                Checksum = "abcdef123456",
                Signature = "VALID_TEST_SIGNATURE",
                MediaSize = 1024,
                VersionCode = 1
            };

            await repo.SaveCampaignAsync(campaign);

            var retrieved = await repo.GetCampaignAsync("CAM-01");
            Assert.NotNull(retrieved);
            Assert.Equal("Sponsor Ad", retrieved.Name);
            Assert.Equal(CampaignPriority.High, retrieved.Priority);
            Assert.Equal("VALID_TEST_SIGNATURE", retrieved.Signature);

            // Fetch Active
            var active = await repo.GetActiveCampaignsAsync();
            Assert.Single(active);
            Assert.Equal("CAM-01", active[0].CampaignId);

            // Update
            campaign.IsDownloaded = true;
            await repo.UpdateCampaignAsync(campaign);

            var updated = await repo.GetCampaignAsync("CAM-01");
            Assert.True(updated.IsDownloaded);

            // Delete
            await repo.DeleteCampaignAsync("CAM-01");
            var deleted = await repo.GetCampaignAsync("CAM-01");
            Assert.Null(deleted);
        }

        #endregion

        #region Download Manager Tests

        [Fact]
        public async Task DownloadManager_Saves_Valid_File_And_Resumes_Interrupted_Download()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            // For first download: regular stream
            var firstContent = "SuperAwesomeCreativeAdvertisementContentOfSponsor";
            var firstChecksum = ComputeSha256(firstContent);

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(firstContent)
                });

            var httpClient = new HttpClient(handlerMock.Object);
            var downloadManager = new AdDownloadManager(httpClient, _downloadLoggerMock.Object);

            var localMediaFile = Path.Combine(_testDbDir, "creative_ad.jpg");

            var campaign = new AdCampaign
            {
                CampaignId = "CAM-DOWNLOAD",
                MediaUrl = "http://example.com/creative.jpg",
                MediaLocalPath = localMediaFile,
                Checksum = firstChecksum,
                MediaSize = firstContent.Length
            };

            // Test successful download and integrity check
            var success = await downloadManager.DownloadMediaAsync(campaign);
            Assert.True(success);
            Assert.True(File.Exists(localMediaFile));
            Assert.Equal(firstContent, File.ReadAllText(localMediaFile));

            // Clean up file for Resume tests
            File.Delete(localMediaFile);

            // Test Download Resuming with Range Headers
            var tempPath = localMediaFile + ".tmp";
            var initialChunk = "SuperAwesome";
            File.WriteAllText(tempPath, initialChunk);

            var remainingChunk = "CreativeAdvertisementContentOfSponsor";

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Headers.Range != null),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.PartialContent,
                    Content = new StringContent(remainingChunk)
                });

            var resumeSuccess = await downloadManager.ResumeDownloadAsync(campaign, tempPath);
            Assert.True(resumeSuccess);
            Assert.True(File.Exists(localMediaFile));
            Assert.Equal(firstContent, File.ReadAllText(localMediaFile));
        }

        [Fact]
        public async Task DownloadManager_Rejects_Corrupted_Media_Checksum_Mismatch()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Tampered Content")
                });

            var httpClient = new HttpClient(handlerMock.Object);
            var downloadManager = new AdDownloadManager(httpClient, _downloadLoggerMock.Object);

            var localFile = Path.Combine(_testDbDir, "corrupted.jpg");
            var campaign = new AdCampaign
            {
                CampaignId = "CAM-CORRUPTED",
                MediaUrl = "http://example.com/bad.jpg",
                MediaLocalPath = localFile,
                Checksum = "invalid_expected_checksum" // mismatch!
            };

            var success = await downloadManager.DownloadMediaAsync(campaign);
            Assert.False(success);
            Assert.False(File.Exists(localFile));
        }

        #endregion

        #region Cache & LRU Eviction Tests

        [Fact]
        public async Task Cache_LRU_Eviction_Under_Quota_Works_Properly()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new AdvertisementRepository(dbService, _repoLoggerMock.Object);
            var cache = new AdvertisementCache(repo, _auditLoggerMock.Object, _cacheLoggerMock.Object);

            await cache.ConfigureQuotaAsync(100); // Super low quota of 100 bytes

            // Prepare 3 downloaded media entries
            var path1 = Path.Combine(_testDbDir, "ad1.jpg");
            var path2 = Path.Combine(_testDbDir, "ad2.jpg");
            var path3 = Path.Combine(_testDbDir, "ad3.jpg");

            File.WriteAllText(path1, "Content1_40Bytes_________"); // 30 bytes
            File.WriteAllText(path2, "Content2_40Bytes_________"); // 30 bytes
            File.WriteAllText(path3, "Content3_40Bytes_________"); // 30 bytes

            var m1 = new DownloadedMedia { MediaPath = path1, CampaignId = "C1", FileSize = 30, LastAccessedAt = DateTime.UtcNow.AddMinutes(-10), Checksum = "c1" };
            var m2 = new DownloadedMedia { MediaPath = path2, CampaignId = "C2", FileSize = 30, LastAccessedAt = DateTime.UtcNow.AddMinutes(-5), Checksum = "c2" };
            var m3 = new DownloadedMedia { MediaPath = path3, CampaignId = "C3", FileSize = 30, LastAccessedAt = DateTime.UtcNow.AddMinutes(-1), Checksum = "c3" };

            await repo.SaveDownloadedMediaAsync(m1);
            await repo.SaveDownloadedMediaAsync(m2);
            await repo.SaveDownloadedMediaAsync(m3);

            // Adding a new file of size 40 bytes -> will exceed 100 bytes total (30+30+30+40 = 130)
            // It needs to evict least-recently-used, which is m1 (C1).
            await cache.EvictLeastRecentlyUsedAsync(40);

            // C1 (least recently used) should be deleted
            Assert.False(File.Exists(path1));
            // C2 and C3 should remain
            Assert.True(File.Exists(path2));
            Assert.True(File.Exists(path3));

            var list = await repo.GetDownloadedMediaListAsync();
            Assert.DoesNotContain(list, x => x.CampaignId == "C1");
        }

        #endregion

        #region Campaign Scheduler & Priority Tests

        [Fact]
        public async Task Scheduler_Selects_Highest_Priority_Active_And_Respects_Daily_Hours()
        {
            var scheduler = new CampaignScheduler();

            var cLow = new AdCampaign
            {
                CampaignId = "LOW-01",
                Priority = CampaignPriority.Low,
                StartTime = DateTime.UtcNow.AddDays(-1),
                EndTime = DateTime.UtcNow.AddDays(1),
                IsDownloaded = true
            };

            var cHigh = new AdCampaign
            {
                CampaignId = "HIGH-01",
                Priority = CampaignPriority.High,
                StartTime = DateTime.UtcNow.AddDays(-1),
                EndTime = DateTime.UtcNow.AddDays(1),
                IsDownloaded = true
            };

            var cEmergency = new AdCampaign
            {
                CampaignId = "EMERGENCY-01",
                Priority = CampaignPriority.Emergency,
                StartTime = DateTime.UtcNow.AddDays(-1),
                EndTime = DateTime.UtcNow.AddDays(1),
                IsDownloaded = true
            };

            var campaigns = new List<AdCampaign> { cLow, cHigh, cEmergency };

            var selected = await scheduler.GetNextPlayableCampaignAsync(campaigns, DateTime.UtcNow);
            Assert.NotNull(selected);
            Assert.Equal("EMERGENCY-01", selected.CampaignId); // Emergency override!

            // Test Daily Active Hours constraints
            var restrictedCampaign = new AdCampaign
            {
                CampaignId = "RESTRICTED-01",
                Priority = CampaignPriority.High,
                StartTime = DateTime.UtcNow.AddDays(-1),
                EndTime = DateTime.UtcNow.AddDays(1),
                DailyActiveHours = "[\"14:00-18:00\"]",
                IsDownloaded = true
            };

            // 15:00 UTC -> should be active
            var testTimeActive = DateTime.UtcNow.Date.AddHours(15);
            Assert.True(scheduler.IsCampaignActiveAtTime(restrictedCampaign, testTimeActive));

            // 12:00 UTC -> should be inactive
            var testTimeInactive = DateTime.UtcNow.Date.AddHours(12);
            Assert.False(scheduler.IsCampaignActiveAtTime(restrictedCampaign, testTimeInactive));
        }

        #endregion

        #region Playback Engine Tests

        [Fact]
        public async Task PlaybackService_Fires_Events_Successfully_On_Start_And_Complete()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new AdvertisementRepository(dbService, _repoLoggerMock.Object);
            var playbackService = new MediaPlaybackService(repo, _auditLoggerMock.Object, _playbackLoggerMock.Object);

            var campaign = new AdCampaign
            {
                CampaignId = "CAM-PLAYBACK",
                DisplayDurationSeconds = 1 // 1 second for fast test
            };

            bool startedFired = false;
            bool completedFired = false;

            playbackService.OnPlaybackStarted += (c) => startedFired = true;
            playbackService.OnPlaybackCompleted += (c) => completedFired = true;

            await playbackService.StartPlaybackAsync(campaign);

            // Wait slightly longer than 1 second display duration
            await Task.Delay(1300);

            Assert.True(startedFired);
            Assert.True(completedFired);

            var history = await repo.GetPlaybackHistoryAsync();
            Assert.Single(history);
            Assert.Equal("CAM-PLAYBACK", history[0].CampaignId);
            Assert.Equal("COMPLETED", history[0].Status);
        }

        #endregion

        #region Impression Tracker Tests

        [Fact]
        public async Task ImpressionTracker_Saves_Impressions_And_Allows_Later_Sync_Query()
        {
            using var dbService = CreateDbService();
            await dbService.InitializeDatabaseAsync();

            var repo = new AdvertisementRepository(dbService, _repoLoggerMock.Object);
            var tracker = new ImpressionTracker(repo, _impressionLoggerMock.Object);

            await tracker.TrackImpressionAsync("CAM-IMP-01", "SESSION-ABC", ImpressionType.CLICK, 5.2);

            var unsynced = await repo.GetUnsyncedImpressionsAsync();
            Assert.Single(unsynced);
            Assert.Equal("CAM-IMP-01", unsynced[0].CampaignId);
            Assert.Equal("SESSION-ABC", unsynced[0].SessionId);
            Assert.Equal(ImpressionType.CLICK, unsynced[0].ImpressionType);
            Assert.Equal(5.2, unsynced[0].PlaybackDurationSeconds);

            // Sync
            await repo.MarkImpressionsAsSyncedAsync(new List<string> { unsynced[0].ImpressionId });
            var remainingUnsynced = await repo.GetUnsyncedImpressionsAsync();
            Assert.Empty(remainingUnsynced);
        }

        #endregion
    }
}
