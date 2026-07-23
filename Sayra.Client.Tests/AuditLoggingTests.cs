using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sayra.Client.Diagnostics.Services;
using Sayra.Client.Discovery.Services;
using Sayra.Client.OfflineQueue;
using Sayra.Client.OfflineQueue.Models;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Logging;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Services;
using SayraClient;
using SayraClient.Commands;
using SayraClient.Services;
using SayraClient.Services.OfflineQueue;
using Xunit;

namespace Sayra.Client.Tests
{
    public class AuditLoggingTests : IDisposable
    {
        private readonly string _testDataDir;

        public AuditLoggingTests()
        {
            _testDataDir = Path.Combine(AppContext.BaseDirectory, "Data");
            CleanTestData();
        }

        private void CleanTestData()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (Directory.Exists(_testDataDir))
            {
                var testDb = Path.Combine(_testDataDir, "audit_logs_test.db");
                if (File.Exists(testDb))
                {
                    try { File.Delete(testDb); } catch {}
                }
                if (File.Exists(testDb + "-wal"))
                {
                    try { File.Delete(testDb + "-wal"); } catch {}
                }
                if (File.Exists(testDb + "-shm"))
                {
                    try { File.Delete(testDb + "-shm"); } catch {}
                }
            }

            var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
            if (Directory.Exists(logsDir))
            {
                try
                {
                    Directory.Delete(logsDir, true);
                }
                catch
                {
                    // Ignore errors
                }
            }
        }

        [Fact]
        public void EventLogEntry_Should_StoreRequiredFields()
        {
            // Arrange & Act
            var entry = new EventLogEntry
            {
                EventId = Guid.NewGuid(),
                CorrelationId = "corr-123",
                SessionId = "sess-456",
                TraceId = "trace-789",
                Category = "SECURITY",
                Severity = "FATAL",
                MessageTemplate = "User failed authentication.",
                PayloadFields = new Dictionary<string, object> { { "User", "admin" } },
                Timestamp = DateTime.UtcNow
            };

            // Assert
            Assert.NotEqual(Guid.Empty, entry.EventId);
            Assert.Equal("corr-123", entry.CorrelationId);
            Assert.Equal("sess-456", entry.SessionId);
            Assert.Equal("trace-789", entry.TraceId);
            Assert.Equal("SECURITY", entry.Category);
            Assert.Equal("FATAL", entry.Severity);
            Assert.Equal("User failed authentication.", entry.MessageTemplate);
            Assert.Equal("admin", entry.PayloadFields["User"]);
        }

        [Fact]
        public async Task TracingContext_Should_IsolateVariables_AcrossAsyncBoundaries()
        {
            // Arrange
            TracingContext.CorrelationId = "main-corr";
            TracingContext.TraceId = "main-trace";

            // Act & Assert
            await Task.Run(async () =>
            {
                Assert.Equal("main-corr", TracingContext.CorrelationId);
                Assert.Equal("main-trace", TracingContext.TraceId);

                TracingContext.CorrelationId = "task-corr";
                TracingContext.TraceId = "task-trace";

                await Task.Delay(50);

                Assert.Equal("task-corr", TracingContext.CorrelationId);
                Assert.Equal("task-trace", TracingContext.TraceId);
            });

            // The parent context should remain untouched due to AsyncLocal isolation
            Assert.Equal("main-corr", TracingContext.CorrelationId);
            Assert.Equal("main-trace", TracingContext.TraceId);
        }

        [Fact]
        public async Task AuditLogRepository_Should_Add_Get_And_Delete_Logs()
        {
            // Arrange
            var repository = new AuditLogRepository("audit_logs_test.db");
            var entry = new EventLogEntry
            {
                EventId = Guid.NewGuid(),
                CorrelationId = "corr-1",
                SessionId = "sess-1",
                TraceId = "trace-1",
                Category = "AUDIT",
                Severity = "INFO",
                MessageTemplate = "Modified kiosk setting {Key} to {Value}",
                PayloadFields = new Dictionary<string, object> { { "Key", "Lockdown" }, { "Value", true } },
                Timestamp = DateTime.UtcNow
            };

            // Act
            await repository.AddLogAsync(entry);

            // Assert
            var count = await repository.GetPendingLogsCountAsync();
            Assert.Equal(1, count);

            var pending = await repository.GetPendingLogsAsync();
            Assert.Single(pending);

            var retrieved = pending[0];
            Assert.Equal(entry.EventId, retrieved.EventId);
            Assert.Equal(entry.CorrelationId, retrieved.CorrelationId);
            Assert.Equal(entry.SessionId, retrieved.SessionId);
            Assert.Equal(entry.TraceId, retrieved.TraceId);
            Assert.Equal(entry.Category, retrieved.Category);
            Assert.Equal(entry.Severity, retrieved.Severity);
            Assert.Equal(entry.MessageTemplate, retrieved.MessageTemplate);
            Assert.Equal("Lockdown", retrieved.PayloadFields["Key"].ToString());

            // Delete
            await repository.DeleteLogsAsync(new List<Guid> { entry.EventId });
            var countAfterDelete = await repository.GetPendingLogsCountAsync();
            Assert.Equal(0, countAfterDelete);
        }

        [Fact]
        public void LogBatchingManager_Should_CompressAndDecompressBatches()
        {
            // Arrange
            var manager = new LogBatchingManager();
            var entries = new List<EventLogEntry>
            {
                new() { CorrelationId = "c1", MessageTemplate = "msg1" },
                new() { CorrelationId = "c2", MessageTemplate = "msg2" }
            };

            // Act
            var compressedBytes = manager.CreateCompressedBatch(entries);
            Assert.NotEmpty(compressedBytes);

            var decompressed = manager.DecompressBatch(compressedBytes);

            // Assert
            Assert.Equal(2, decompressed.Count);
            Assert.Equal("msg1", decompressed[0].MessageTemplate);
            Assert.Equal("msg2", decompressed[1].MessageTemplate);
        }

        [Fact]
        public async Task AuditLogger_Should_ResolveContext_And_PublishAndPersistEvents()
        {
            // Arrange
            var mockSessionContext = new Mock<ISessionContextProvider>();
            mockSessionContext.Setup(x => x.CurrentSessionId).Returns("session-xyz");

            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(x => x.GetService(typeof(ISessionContextProvider))).Returns(mockSessionContext.Object);

            var eventDispatcher = new EventDispatcher();
            var repository = new AuditLogRepository("audit_logs_test.db");

            var auditLogger = new AuditLogger(
                mockServiceProvider.Object,
                eventDispatcher,
                repository,
                NullLogger<AuditLogger>.Instance
            );

            TracingContext.CorrelationId = "corr-abc";
            TracingContext.TraceId = "trace-def";

            EventLogEntry? dispatchedEntry = null;
            eventDispatcher.RegisterHandler<EventLogEntry>(e => dispatchedEntry = e);

            // Act
            auditLogger.LogAudit("Operation complete. Result: {Success}", new Dictionary<string, object> { { "Success", true } });

            // Assert
            Assert.NotNull(dispatchedEntry);
            Assert.Equal("corr-abc", dispatchedEntry.CorrelationId);
            Assert.Equal("trace-def", dispatchedEntry.TraceId);
            Assert.Equal("session-xyz", dispatchedEntry.SessionId);
            Assert.Equal("AUDIT", dispatchedEntry.Category);
            Assert.Equal("INFO", dispatchedEntry.Severity);

            var pendingLogs = await repository.GetPendingLogsAsync();
            Assert.Single(pendingLogs);
            Assert.Equal("corr-abc", pendingLogs[0].CorrelationId);
        }

        private Mock<TcpClientManager> CreateMockTcpClientManager()
        {
            var mockConfig = new Mock<IConfiguration>();
            var mockReconnect = new Mock<ReconnectManager>(NullLogger<ReconnectManager>.Instance, 2000, 30000);
            var mockParser = new Mock<CommandParser>(NullLogger<CommandParser>.Instance);
            var mockRouter = new Mock<CommandRouter>(new List<ICommandHandler>(), NullLogger<CommandRouter>.Instance);

            var mockSP = new Mock<IServiceProvider>();
            var mockKey = new Mock<SessionKeyManager>();
            var mockEncrypt = new Mock<EncryptionManager>(NullLogger<EncryptionManager>.Instance, mockKey.Object);
            var mockIntegrity = new Mock<IntegrityValidator>(NullLogger<IntegrityValidator>.Instance, mockKey.Object);
            var mockTransport = new Mock<SecureTransportLayer>(NullLogger<SecureTransportLayer>.Instance, mockEncrypt.Object, mockIntegrity.Object, mockKey.Object);

            var mockVal = new Mock<SecureMessageValidator>(NullLogger<SecureMessageValidator>.Instance);
            var mockState = new Mock<ClientStateManager>(NullLogger<ClientStateManager>.Instance, mockSP.Object);
            var mockAuth = new Mock<AuthManager>(NullLogger<AuthManager>.Instance, mockConfig.Object, mockKey.Object);

            var mockHandler = new Mock<MessageHandler>(
                NullLogger<MessageHandler>.Instance,
                mockParser.Object,
                mockRouter.Object,
                mockVal.Object,
                mockTransport.Object,
                mockAuth.Object,
                mockState.Object
            );

            var mockDisc = new Mock<IDiscoveryService>();

            return new Mock<TcpClientManager>(
                NullLogger<TcpClientManager>.Instance,
                mockConfig.Object,
                mockReconnect.Object,
                mockHandler.Object,
                mockSP.Object,
                mockTransport.Object,
                mockKey.Object,
                mockAuth.Object,
                mockState.Object,
                mockDisc.Object
            );
        }

        [Fact]
        public async Task EventQueueBatchingWorker_Should_SendBatchesDirectly_WhenOnline()
        {
            // Arrange
            var repository = new AuditLogRepository("audit_logs_test.db");
            var batchingManager = new LogBatchingManager();
            var eventDispatcher = new EventDispatcher();

            var logs = new List<EventLogEntry>
            {
                new() { CorrelationId = "c1", MessageTemplate = "log1" },
                new() { CorrelationId = "c2", MessageTemplate = "log2" }
            };
            foreach (var log in logs)
            {
                await repository.AddLogAsync(log);
            }

            var mockTcpClientManager = CreateMockTcpClientManager();
            mockTcpClientManager.Setup(x => x.IsConnected).Returns(true);
            mockTcpClientManager.Setup(x => x.SendMessageAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var mockOfflineQueue = new Mock<IOfflineQueueManager>();

            var worker = new EventQueueBatchingWorker(
                NullLogger<EventQueueBatchingWorker>.Instance,
                new Mock<IServiceHealthMonitor>().Object,
                repository,
                batchingManager,
                mockTcpClientManager.Object,
                mockOfflineQueue.Object,
                eventDispatcher
            );

            // Act
            worker.TriggerFlush();
            using var cts = new CancellationTokenSource();
            var runTask = worker.RunSupervisedAsync(cts.Token);
            await Task.Delay(150);
            cts.Cancel();
            try { await runTask; } catch (OperationCanceledException) { }

            // Assert
            mockTcpClientManager.Verify(x => x.SendMessageAsync(It.Is<object>(e => e is ClientEvent && ((ClientEvent)e).EventType == "LOG_BATCH"), It.IsAny<CancellationToken>()), Times.Once);
            mockOfflineQueue.Verify(x => x.AddEventAsync(It.IsAny<ClientEvent>()), Times.Never);

            var pendingCount = await repository.GetPendingLogsCountAsync();
            Assert.Equal(0, pendingCount); // Successfully deleted/flushed
        }

        [Fact]
        public async Task EventQueueBatchingWorker_Should_FallbackToOfflineQueue_WhenOffline()
        {
            // Arrange
            var repository = new AuditLogRepository("audit_logs_test.db");
            var batchingManager = new LogBatchingManager();
            var eventDispatcher = new EventDispatcher();

            var logs = new List<EventLogEntry>
            {
                new() { CorrelationId = "c1", MessageTemplate = "log1" }
            };
            foreach (var log in logs)
            {
                await repository.AddLogAsync(log);
            }

            var mockTcpClientManager = CreateMockTcpClientManager();
            mockTcpClientManager.Setup(x => x.IsConnected).Returns(false);

            var mockOfflineQueue = new Mock<IOfflineQueueManager>();
            mockOfflineQueue.Setup(x => x.AddEventAsync(It.IsAny<ClientEvent>())).Returns(Task.CompletedTask);

            var worker = new EventQueueBatchingWorker(
                NullLogger<EventQueueBatchingWorker>.Instance,
                new Mock<IServiceHealthMonitor>().Object,
                repository,
                batchingManager,
                mockTcpClientManager.Object,
                mockOfflineQueue.Object,
                eventDispatcher
            );

            // Act
            worker.TriggerFlush();
            using var cts = new CancellationTokenSource();
            var runTask = worker.RunSupervisedAsync(cts.Token);
            await Task.Delay(150);
            cts.Cancel();
            try { await runTask; } catch (OperationCanceledException) { }

            // Assert
            mockOfflineQueue.Verify(x => x.AddEventAsync(It.Is<ClientEvent>(e => e.EventType == "LOG_BATCH")), Times.Once);

            var pendingCount = await repository.GetPendingLogsCountAsync();
            Assert.Equal(0, pendingCount); // Emptied from buffer and saved to encrypted queue
        }

        [Fact]
        public async Task LogCompressionWorker_Should_CompressAndPruneRotatedLogs()
        {
            // Arrange
            var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            // Create client.log (active) and rotated client_1.log, client_2.log
            var activePath = Path.Combine(logsDir, "client.log");
            await File.WriteAllTextAsync(activePath, "Active log content");

            var rotated1 = Path.Combine(logsDir, "client_1.log");
            await File.WriteAllTextAsync(rotated1, "Rotated 1 content");

            var rotated2 = Path.Combine(logsDir, "client_2.log");
            await File.WriteAllTextAsync(rotated2, "Rotated 2 content");

            var worker = new LogCompressionWorker(
                NullLogger<LogCompressionWorker>.Instance,
                new Mock<IServiceHealthMonitor>().Object
            );

            // Act
            worker.CompressAndPruneLogs();

            // Assert
            Assert.True(File.Exists(activePath)); // Active stays untouched
            Assert.False(File.Exists(rotated1)); // Rotated 1 is compressed and deleted
            Assert.False(File.Exists(rotated2)); // Rotated 2 is compressed and deleted

            Assert.True(File.Exists(rotated1 + ".gz"));
            Assert.True(File.Exists(rotated2 + ".gz"));

            // Verify GZip decompression matches
            using (var fs = File.OpenRead(rotated1 + ".gz"))
            using (var gzs = new GZipStream(fs, CompressionMode.Decompress))
            using (var ms = new MemoryStream())
            {
                await gzs.CopyToAsync(ms);
                var content = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                Assert.Equal("Rotated 1 content", content);
            }
        }

        public void Dispose()
        {
            CleanTestData();
        }
    }
}
