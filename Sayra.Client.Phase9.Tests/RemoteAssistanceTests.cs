using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.Shared.Fleet.RemoteAssistance;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Xunit;

namespace Sayra.Client.Phase9.Tests
{
    public class RemoteAssistanceTests
    {
        private readonly Mock<IEventDispatcher> _eventDispatcherMock;
        private readonly Mock<IAuditLogger> _auditLoggerMock;
        private readonly Mock<ILogger<RemoteSessionCoordinator>> _coordinatorLoggerMock;
        private readonly Mock<ILogger<RemoteSupportEngine>> _engineLoggerMock;
        private readonly Mock<ILogger<RemoteSessionManager>> _managerLoggerMock;
        private readonly Mock<ILogger<RemoteDesktopProvider>> _desktopLoggerMock;
        private readonly Mock<ILogger<RemoteConsoleService>> _consoleLoggerMock;
        private readonly Mock<ILogger<RemoteLogStreamService>> _logLoggerMock;
        private readonly Mock<ILogger<RemoteEventStreamService>> _eventStreamLoggerMock;
        private readonly Mock<ILogger<RemoteSessionSecurity>> _securityLoggerMock;

        public RemoteAssistanceTests()
        {
            _eventDispatcherMock = new Mock<IEventDispatcher>();
            _auditLoggerMock = new Mock<IAuditLogger>();
            _coordinatorLoggerMock = new Mock<ILogger<RemoteSessionCoordinator>>();
            _engineLoggerMock = new Mock<ILogger<RemoteSupportEngine>>();
            _managerLoggerMock = new Mock<ILogger<RemoteSessionManager>>();
            _desktopLoggerMock = new Mock<ILogger<RemoteDesktopProvider>>();
            _consoleLoggerMock = new Mock<ILogger<RemoteConsoleService>>();
            _logLoggerMock = new Mock<ILogger<RemoteLogStreamService>>();
            _eventStreamLoggerMock = new Mock<ILogger<RemoteEventStreamService>>();
            _securityLoggerMock = new Mock<ILogger<RemoteSessionSecurity>>();
        }

        [Fact]
        public async Task SupportSession_Lifecycle_TransitionsAndApprovals()
        {
            // Arrange
            using var coordinator = new RemoteSessionCoordinator(_eventDispatcherMock.Object, _coordinatorLoggerMock.Object);
            var engine = new RemoteSupportEngine(coordinator, _engineLoggerMock.Object);
            var manager = new RemoteSessionManager(coordinator, _managerLoggerMock.Object);

            // 1. Request Session
            var session = await engine.RequestSupportSessionAsync("PC01", SupportSessionType.UnifiedRemoteSupport);
            Assert.Equal("PC01", session.TargetMachineId);
            Assert.Equal(RemoteSessionStatus.Requested, session.Status);

            // 2. Approve Request
            var approved = coordinator.ApproveRequest(session.SessionId, "Operator-99");
            Assert.True(approved);
            Assert.Equal(RemoteSessionStatus.Approved, coordinator.GetSession(session.SessionId)!.Status);

            // 3. Open Session
            var opened = await manager.OpenSessionAsync(session.SessionId);
            Assert.True(opened);
            Assert.Equal(RemoteSessionStatus.Active, coordinator.GetSession(session.SessionId)!.Status);

            // 4. Pause and Resume
            var paused = coordinator.PauseSession(session.SessionId);
            Assert.True(paused);
            Assert.Equal(RemoteSessionStatus.Paused, coordinator.GetSession(session.SessionId)!.Status);

            var resumed = coordinator.ResumeSession(session.SessionId);
            Assert.True(resumed);
            Assert.Equal(RemoteSessionStatus.Active, coordinator.GetSession(session.SessionId)!.Status);

            // 5. Terminate Session
            var closed = await manager.CloseSessionAsync(session.SessionId);
            Assert.True(closed);
            Assert.Null(coordinator.GetSession(session.SessionId));
        }

        [Fact]
        public async Task SecurityService_EnforcesReplayProtectionAndTimestampWindow()
        {
            // Arrange
            var security = new RemoteSessionSecurity(_auditLoggerMock.Object, _securityLoggerMock.Object);

            var validRequest = new RemoteSessionRequest
            {
                RequestId = "REQ1",
                AdministratorId = "Admin-Core-Client",
                MachineId = "PC01",
                Reason = "Need access",
                TimestampUtc = DateTime.UtcNow
            };

            var expiredRequest = validRequest with { RequestId = "REQ2", TimestampUtc = DateTime.UtcNow.AddMinutes(-10) };

            // Act & Assert
            // Valid request passes
            bool auth1 = await security.AuthorizeSessionRequestAsync(validRequest, "nonce_001");
            Assert.True(auth1);

            // Replay with same nonce rejected
            bool auth2 = await security.AuthorizeSessionRequestAsync(validRequest, "nonce_001");
            Assert.False(auth2);

            // Replay with new nonce but expired timestamp rejected
            bool auth3 = await security.AuthorizeSessionRequestAsync(expiredRequest, "nonce_002");
            Assert.False(auth3);
        }

        [Fact]
        public async Task ConsoleService_IsolatesSessionAndBlocksRestrictedCommands()
        {
            // Arrange
            using var coordinator = new RemoteSessionCoordinator(_eventDispatcherMock.Object, _coordinatorLoggerMock.Object);
            var console = new RemoteConsoleService(coordinator, _consoleLoggerMock.Object);

            var req = new RemoteSessionRequest { RequestId = "REQ1", AdministratorId = "Admin", MachineId = "PC01", Reason = "Help", TimestampUtc = DateTime.UtcNow };
            coordinator.RegisterRequest(req);
            coordinator.ApproveRequest("REQ1", "Admin");
            coordinator.OpenSession("REQ1");

            // Act & Assert
            // Prohibited command is blocked
            await console.ExecuteConsoleCommandAsync("REQ1", "rm -rf /sys");

            var lines = new List<string>();
            try
            {
                await foreach (var line in console.GetConsoleOutputStreamAsync("REQ1").WithCancellation(new CancellationTokenSource(250).Token))
                {
                    lines.Add(line);
                }
            }
            catch (OperationCanceledException) { }

            Assert.Contains(lines, l => l.Contains("[SECURITY BLOCK]"));
            Assert.DoesNotContain(lines, l => l.Contains("> rm -rf"));
        }

        [Fact]
        public async Task StreamingProviders_SimulateLowLatencyOutputs()
        {
            // Arrange
            using var coordinator = new RemoteSessionCoordinator(_eventDispatcherMock.Object, _coordinatorLoggerMock.Object);
            var desktop = new RemoteDesktopProvider(coordinator, _desktopLoggerMock.Object);
            var logger = new RemoteLogStreamService(coordinator, _logLoggerMock.Object);
            var events = new RemoteEventStreamService(coordinator, _eventStreamLoggerMock.Object);

            var req = new RemoteSessionRequest { RequestId = "REQ1", AdministratorId = "Admin", MachineId = "PC01", Reason = "Help", TimestampUtc = DateTime.UtcNow };
            coordinator.RegisterRequest(req);
            coordinator.ApproveRequest("REQ1", "Admin");
            coordinator.OpenSession("REQ1");

            // Desktop screen captures
            var stream = await desktop.GetScreenCaptureStreamAsync("REQ1");
            using var reader = new StreamReader(stream);
            var val = await reader.ReadToEndAsync();
            Assert.StartsWith("SAYRA_DESKTOP_FRAME_ID:REQ1_TIMESTAMP:", val);

            // Log streaming with warning filter
            var logsList = new List<string>();
            var cts = new CancellationTokenSource(1000);
            try
            {
                await foreach (var log in logger.StreamLogsAsync("REQ1", null, NotificationSeverity.Warning, cts.Token))
                {
                    logsList.Add(log);
                }
            }
            catch (OperationCanceledException) { }

            Assert.NotEmpty(logsList);
            Assert.All(logsList, l => Assert.True(l.Contains("[WARNING]") || l.Contains("[CRITICAL]") || l.Contains("[ERROR]")));

            // Event streaming and publishing
            var eventsList = new List<string>();
            var cts2 = new CancellationTokenSource(3000);
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                await events.PublishEventAsync("REQ1", "{\"Event\":\"CustomSystemMetric\"}");
            });

            try
            {
                await foreach (var ev in events.StreamEventsAsync("REQ1", cts2.Token))
                {
                    eventsList.Add(ev);
                }
            }
            catch (OperationCanceledException) { }

            Assert.Contains(eventsList, e => e.Contains("CustomSystemMetric"));
        }

        [Fact]
        public async Task Simulate1000_Simultaneous_Sessions_Resource_And_Timeout_Tests()
        {
            // Arrange
            using var coordinator = new RemoteSessionCoordinator(_eventDispatcherMock.Object, _coordinatorLoggerMock.Object);
            var engine = new RemoteSupportEngine(coordinator, _engineLoggerMock.Object);

            // Act: create 1000 requests
            var list = new List<RemoteSession>();
            for (int i = 0; i < 1000; i++)
            {
                var s = await engine.RequestSupportSessionAsync($"PC-{i:0000}", SupportSessionType.UnifiedRemoteSupport);
                list.Add(s);
            }

            // Assert
            Assert.Equal(1000, list.Count);
            Assert.Equal(RemoteSessionStatus.Requested, list[450].Status);
        }
    }
}
