using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sayra.Client.OfflineQueue;
using Sayra.Client.OfflineQueue.Models;
using Sayra.Client.OfflineQueue.Security;
using Sayra.Client.OfflineQueue.Serialization;
using Xunit;

namespace Sayra.Client.Tests;

public class OfflineQueueTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly IConfiguration _mockConfiguration;

    public OfflineQueueTests()
    {
        _testDataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        // Ensure clean slate before each test
        CleanTestData();

        var inMemorySettings = new System.Collections.Generic.Dictionary<string, string> {
            {"OfflineQueue:MaxRetries", "3"}
        };
        _mockConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    private void CleanTestData()
    {
        if (Directory.Exists(_testDataDir))
        {
            try
            {
                Directory.Delete(_testDataDir, true);
            }
            catch
            {
                // Ignore lock/delete errors in test teardown
            }
        }
    }

    [Fact]
    public void EventBaseModel_Should_StoreRequiredProperties_And_ValidateCorrectly()
    {
        // Arrange & Act
        var evt = new ClientEvent
        {
            EventId = "test-id",
            EventType = "USER_LOGIN",
            EventVersion = "1.0.0",
            ClientId = "client-a",
            MachineId = "machine-123",
            SessionId = "session-456",
            CorrelationId = "corr-789",
            TraceId = "trace-000",
            CreatedAt = DateTime.UtcNow,
            Priority = QueuePriority.HIGH,
            Payload = "{\"user\":\"john\"}",
            Signature = "sig-abc"
        };

        // Assert
        Assert.Equal("test-id", evt.EventId);
        Assert.Equal("USER_LOGIN", evt.EventType);
        Assert.Equal("1.0.0", evt.EventVersion);
        Assert.Equal("client-a", evt.ClientId);
        Assert.Equal("machine-123", evt.MachineId);
        Assert.Equal("session-456", evt.SessionId);
        Assert.Equal("corr-789", evt.CorrelationId);
        Assert.Equal("trace-000", evt.TraceId);
        Assert.Equal(QueuePriority.HIGH, evt.Priority);
        Assert.Equal("{\"user\":\"john\"}", evt.Payload);
        Assert.Equal("sig-abc", evt.Signature);

        // Validation check should pass
        evt.Validate();
    }

    [Fact]
    public void EventBaseModel_Should_ThrowException_When_RequiredFieldsAreMissing()
    {
        // Arrange & Act & Assert
        var evt1 = new ClientEvent { EventId = "" };
        Assert.Throws<ArgumentException>(() => evt1.Validate());

        var evt2 = new ClientEvent { EventType = "" };
        Assert.Throws<ArgumentException>(() => evt2.Validate());

        var evt3 = new ClientEvent { Payload = "" };
        Assert.Throws<ArgumentException>(() => evt3.Validate());
    }

    [Fact]
    public void EventSerializer_Should_Serialize_And_Deserialize_Correctly()
    {
        // Arrange
        var serializer = new EventSerializer();
        var evt = new ClientEvent
        {
            EventId = "evt-111",
            EventType = "GAME_START",
            EventVersion = "1.2.3",
            Payload = "{\"gameId\":\"wow\"}"
        };

        // Act
        var json = serializer.Serialize(evt);
        var deserialized = serializer.Deserialize(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(evt.EventId, deserialized.EventId);
        Assert.Equal(evt.EventType, deserialized.EventType);
        Assert.Equal(evt.EventVersion, deserialized.EventVersion);
        Assert.Equal(evt.Payload, deserialized.Payload);
    }

    [Fact]
    public void EventSerializer_Should_Check_VersionCompatibility()
    {
        // Arrange
        var serializer = new EventSerializer();

        // Act & Assert
        Assert.True(serializer.IsCompatible("1.0.0"));
        Assert.True(serializer.IsCompatible("1.5.2"));
        Assert.False(serializer.IsCompatible("2.0.0")); // Different major version is incompatible
        Assert.False(serializer.IsCompatible("abc"));
    }

    [Fact]
    public void QueueSecurityManager_Should_EncryptDecryptPayload_And_GenerateValidSignatures()
    {
        // Arrange
        var securityManager = new QueueSecurityManager(NullLogger<QueueSecurityManager>.Instance);
        var originalPayload = "Highly sensitive gaming transaction data here!";

        // Act
        var encrypted = securityManager.EncryptPayload(originalPayload);
        var decrypted = securityManager.DecryptPayload(encrypted);

        var signature = securityManager.GenerateSignature(originalPayload);
        var verifyTrue = securityManager.VerifySignature(originalPayload, signature);
        var verifyFalse = securityManager.VerifySignature(originalPayload + "tampered", signature);

        // Assert
        Assert.NotEqual(originalPayload, encrypted);
        Assert.Equal(originalPayload, decrypted);
        Assert.True(verifyTrue);
        Assert.False(verifyFalse);
    }

    [Fact]
    public async Task OfflineQueueManager_Should_Add_Get_And_CompleteEvents_With_PriorityOrdering()
    {
        // Arrange
        var serializer = new EventSerializer();
        var securityManager = new QueueSecurityManager(NullLogger<QueueSecurityManager>.Instance);
        using var queueManager = new OfflineQueueManager(
            NullLogger<OfflineQueueManager>.Instance,
            serializer,
            securityManager,
            _mockConfiguration);

        var lowEvt = new ClientEvent { EventType = "TELEMETRY", Priority = QueuePriority.LOW, Payload = "low" };
        var normalEvt = new ClientEvent { EventType = "SYSTEM_LOG", Priority = QueuePriority.NORMAL, Payload = "normal" };
        var criticalEvt = new ClientEvent { EventType = "BILLING_TRANSACTION", Priority = QueuePriority.CRITICAL, Payload = "critical" };
        var highEvt = new ClientEvent { EventType = "SECURITY_ALERT", Priority = QueuePriority.HIGH, Payload = "high" };

        // Act - Insert in non-priority order
        await queueManager.AddEventAsync(lowEvt);
        await queueManager.AddEventAsync(normalEvt);
        await queueManager.AddEventAsync(criticalEvt);
        await queueManager.AddEventAsync(highEvt);

        // Retrieve pending events
        var pending = await queueManager.GetPendingEventsAsync(limit: 10);

        // Assert - Deterministic ordering (CRITICAL > HIGH > NORMAL > LOW)
        Assert.Equal(4, pending.Count);
        Assert.Equal("BILLING_TRANSACTION", pending[0].EventType);
        Assert.Equal("SECURITY_ALERT", pending[1].EventType);
        Assert.Equal("SYSTEM_LOG", pending[2].EventType);
        Assert.Equal("TELEMETRY", pending[3].EventType);

        // Check encryption in storage (raw payload should be encrypted, not matching "critical")
        Assert.NotEqual("critical", pending[0].Payload);

        // Mark completed
        var criticalId = pending[0].Id;
        await queueManager.MarkCompletedAsync(criticalId);

        // Verify it is no longer pending
        var pendingAfterComplete = await queueManager.GetPendingEventsAsync(limit: 10);
        Assert.Equal(3, pendingAfterComplete.Count);
        Assert.DoesNotContain(pendingAfterComplete, x => x.Id == criticalId);
    }

    [Fact]
    public void RetryEngine_Should_CalculateExponentialBackoff_UsingBaseThree()
    {
        // Arrange & Act & Assert
        // Attempt 1: 3 seconds
        Assert.Equal(TimeSpan.FromSeconds(3), OfflineQueueManager.GetBackoffDelay(1));
        // Attempt 2: 9 seconds
        Assert.Equal(TimeSpan.FromSeconds(9), OfflineQueueManager.GetBackoffDelay(2));
        // Attempt 3: 27 seconds
        Assert.Equal(TimeSpan.FromSeconds(27), OfflineQueueManager.GetBackoffDelay(3));
        // Attempt 4: 81 seconds
        Assert.Equal(TimeSpan.FromSeconds(81), OfflineQueueManager.GetBackoffDelay(4));
        // Attempt 5: 243 seconds
        Assert.Equal(TimeSpan.FromSeconds(243), OfflineQueueManager.GetBackoffDelay(5));
        // Attempt 6: Maxes out at 300 seconds
        Assert.Equal(TimeSpan.FromSeconds(300), OfflineQueueManager.GetBackoffDelay(6));
    }

    [Fact]
    public async Task DeadLetterQueue_Should_IsolateEvents_AfterMaxRetriesExceeded()
    {
        // Arrange
        var serializer = new EventSerializer();
        var securityManager = new QueueSecurityManager(NullLogger<QueueSecurityManager>.Instance);
        using var queueManager = new OfflineQueueManager(
            NullLogger<OfflineQueueManager>.Instance,
            serializer,
            securityManager,
            _mockConfiguration);

        var evt = new ClientEvent { EventType = "CRASH_REPORT", Payload = "payload-data" };
        await queueManager.AddEventAsync(evt);

        var pending = await queueManager.GetPendingEventsAsync();
        var item = pending.First();

        // Act - Simulate failures up to max limit (e.g. 3 attempts)
        await queueManager.RecordFailureAsync(item.Id, "Network failure 1", maxRetries: 3);
        await queueManager.RecordFailureAsync(item.Id, "Network failure 2", maxRetries: 3);
        await queueManager.RecordFailureAsync(item.Id, "Network failure 3", maxRetries: 3); // Exceeds limit

        // Assert
        var pendingAfter = await queueManager.GetPendingEventsAsync();
        Assert.Empty(pendingAfter); // Is isolated from active queue

        var dlqItems = await queueManager.GetDeadLetterItemsAsync();
        Assert.Single(dlqItems); // Promoted to DLQ

        var dlqItem = dlqItems.First();
        Assert.Equal(item.Id, dlqItem.OriginalQueueItemId);
        Assert.Equal("CRASH_REPORT", dlqItem.EventType);
        Assert.Contains("Network failure 3", dlqItem.ErrorReason);
    }

    [Fact]
    public async Task CorruptionRecovery_Should_DetectIntegrityFailure_BackupCorruptedFile_And_RecreateDatabase()
    {
        // Arrange
        var serializer = new EventSerializer();
        var securityManager = new QueueSecurityManager(NullLogger<QueueSecurityManager>.Instance);

        string dbPath;
        using (var queueManager = new OfflineQueueManager(
            NullLogger<OfflineQueueManager>.Instance,
            serializer,
            securityManager,
            _mockConfiguration))
        {
            var evt = new ClientEvent { EventType = "DUMMY", Payload = "dummy" };
            await queueManager.AddEventAsync(evt);
            var pending = await queueManager.GetPendingEventsAsync();
            Assert.Single(pending);

            dbPath = Path.Combine(_testDataDir, "offline_queue.db");
            Assert.True(File.Exists(dbPath));
        }

        // Create new queue manager instance
        using (var queueManagerRecovered = new OfflineQueueManager(
            NullLogger<OfflineQueueManager>.Instance,
            serializer,
            securityManager,
            _mockConfiguration))
        {
            var p = await queueManagerRecovered.GetPendingEventsAsync();
            Assert.Single(p);

            // Act - Corrupt the SQLite database file on disk while the manager is active
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            await File.WriteAllBytesAsync(dbPath, EncodingEquals("This database is fully corrupted and malformed raw text!"));

            // Delete WAL files to prevent recovery from WAL
            if (File.Exists(dbPath + "-wal")) File.Delete(dbPath + "-wal");
            if (File.Exists(dbPath + "-shm")) File.Delete(dbPath + "-shm");

            // Verify integrity check detects the corruption
            bool integrity = await queueManagerRecovered.VerifyIntegrityAsync();
            Assert.False(integrity);

            // Recreate database
            await queueManagerRecovered.ForceRecreateDatabaseAsync();

            // Assert
            bool integrityRecovered = await queueManagerRecovered.VerifyIntegrityAsync();
            Assert.True(integrityRecovered); // Recreated database is healthy

            var pendingRecovered = await queueManagerRecovered.GetPendingEventsAsync();
            Assert.Empty(pendingRecovered); // Clean slate recreated
        }

        // Verify corrupted backup file exists
        var corruptedFiles = Directory.GetFiles(_testDataDir, "*.corrupted.*");
        Assert.NotEmpty(corruptedFiles);
    }

    private static byte[] EncodingEquals(string s) => System.Text.Encoding.UTF8.GetBytes(s);

    public void Dispose()
    {
        CleanTestData();
    }
}
