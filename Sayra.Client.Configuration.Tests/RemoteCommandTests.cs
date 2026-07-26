using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.Launcher.Services;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Models;
using SayraClient.Kiosk.Application.Interfaces;
using SayraClient.RemoteOperations.Handlers;
using SayraClient.RemoteOperations.Security;
using SayraClient.RemoteOperations.Services;
using SayraClient.Services;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    public class RemoteCommandTests
    {
        private readonly Mock<IAuditLogger> _auditLoggerMock;
        private readonly Mock<ILogger<RemoteCommandDispatcher>> _dispatcherLoggerMock;
        private readonly Mock<ILogger<RemoteCommandEngine>> _engineLoggerMock;
        private readonly Mock<ILogger<CommandResultReporter>> _reporterLoggerMock;
        private readonly Mock<IServiceHealthMonitor> _healthMonitorMock;

        // Handler Dependency Mocks
        private readonly Mock<IPowerManagementService> _powerMock;
        private readonly Mock<IGameLauncherService> _launcherMock;
        private readonly Mock<IMaintenanceModeService> _maintenanceMock;

        private readonly CryptoService _cryptoService;
        private readonly SignatureVerifier _signatureVerifier;
        private readonly MessageAuthenticator _messageAuthenticator;

        private readonly byte[] _aesKey;
        private readonly byte[] _hmacKey;
        private readonly RSA _rsa;
        private readonly string _publicKeyPem;
        private readonly byte[] _privateKeyBytes;

        public RemoteCommandTests()
        {
            _auditLoggerMock = new Mock<IAuditLogger>();
            _dispatcherLoggerMock = new Mock<ILogger<RemoteCommandDispatcher>>();
            _engineLoggerMock = new Mock<ILogger<RemoteCommandEngine>>();
            _reporterLoggerMock = new Mock<ILogger<CommandResultReporter>>();
            _healthMonitorMock = new Mock<IServiceHealthMonitor>();

            _powerMock = new Mock<IPowerManagementService>();
            _launcherMock = new Mock<IGameLauncherService>();
            _maintenanceMock = new Mock<IMaintenanceModeService>();

            _cryptoService = new CryptoService();
            _signatureVerifier = new SignatureVerifier();
            _messageAuthenticator = new MessageAuthenticator();

            // Generate Keys
            _aesKey = RandomNumberGenerator.GetBytes(32);
            _hmacKey = RandomNumberGenerator.GetBytes(32);

            _rsa = RSA.Create();
            _publicKeyPem = _rsa.ExportRSAPublicKeyPem();
            _privateKeyBytes = _rsa.ExportPkcs8PrivateKey();
        }

        private RemoteCommandDispatcher CreateDispatcher(IEnumerable<IRemoteCommandHandler> handlers)
        {
            var dispatcher = new RemoteCommandDispatcher(
                handlers,
                _cryptoService,
                _signatureVerifier,
                _messageAuthenticator,
                _auditLoggerMock.Object,
                _dispatcherLoggerMock.Object
            );
            dispatcher.PublicKeyPem = _publicKeyPem;
            return dispatcher;
        }

        private string SignCommand(Guid id, string action, string sender, DateTime timestamp, string payload, string nonce)
        {
            string canonical = $"{id}:{action}:{sender}:{timestamp:O}:{payload}:{nonce}";
            byte[] dataBytes = Encoding.UTF8.GetBytes(canonical);
            byte[] sigBytes = _rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(sigBytes);
        }

        #region Dispatcher & Handler Routing Tests

        [Fact]
        public async Task Dispatcher_ShouldSelectAndExecuteCorrectHandler()
        {
            _powerMock.Setup(p => p.LockWorkstationAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var handler = new LockPcCommandHandler(_powerMock.Object);
            var dispatcher = CreateDispatcher(new[] { handler });

            var commandId = Guid.NewGuid();
            var timestamp = DateTime.UtcNow;
            var sig = SignCommand(commandId, "LOCK_PC", "Admin_01", timestamp, "", "nonce123");

            var command = new RemoteCommand
            {
                CommandId = commandId,
                Action = "LOCK_PC",
                SenderAdminId = "Admin_01",
                Timestamp = timestamp,
                Signature = sig,
                Nonce = "nonce123",
                ExpirationTime = DateTime.UtcNow.AddMinutes(5)
            };

            var result = await dispatcher.DispatchAsync(command, CancellationToken.None);

            Assert.True(result.Success);
            _powerMock.Verify(p => p.LockWorkstationAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Dispatcher_ShouldRejectUnknownAction()
        {
            var dispatcher = CreateDispatcher(Array.Empty<IRemoteCommandHandler>());

            var commandId = Guid.NewGuid();
            var timestamp = DateTime.UtcNow;
            var sig = SignCommand(commandId, "UNKNOWN_ACTION", "Admin_01", timestamp, "", "nonce123");

            var command = new RemoteCommand
            {
                CommandId = commandId,
                Action = "UNKNOWN_ACTION",
                SenderAdminId = "Admin_01",
                Timestamp = timestamp,
                Signature = sig,
                Nonce = "nonce123",
                ExpirationTime = DateTime.UtcNow.AddMinutes(5)
            };

            var result = await dispatcher.DispatchAsync(command, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("UNKNOWN_ACTION", result.ErrorCode);
        }

        #endregion

        #region Security Validation Pipeline Tests

        [Fact]
        public async Task SecurityPipeline_ValidFrame_ShouldSucceed()
        {
            _launcherMock.Setup(l => l.LaunchGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var handler = new LaunchGameCommandHandler(_launcherMock.Object);
            var dispatcher = CreateDispatcher(new[] { handler });

            var commandId = Guid.NewGuid();
            var timestamp = DateTime.UtcNow;
            var payloadJson = "{\"gameId\":\"DOTA_2\"}";
            var sig = SignCommand(commandId, "LAUNCH_GAME", "Admin_01", timestamp, payloadJson, "nonce123");

            var envelope = new CommandEnvelope
            {
                CommandId = commandId.ToString(),
                Action = "LAUNCH_GAME",
                SenderAdminId = "Admin_01",
                Timestamp = timestamp,
                Payload = payloadJson,
                Priority = "High",
                Signature = sig,
                ExpirationTime = DateTime.UtcNow.AddMinutes(5),
                Nonce = "nonce123"
            };

            var envelopeJson = JsonSerializer.Serialize(envelope);
            byte[] plainBytes = Encoding.UTF8.GetBytes(envelopeJson);
            byte[] encrypted = _cryptoService.Encrypt(plainBytes, _aesKey, new byte[16]);
            byte[] hmac = _messageAuthenticator.ComputeHmac(encrypted, _hmacKey);

            var frame = new SecureMessageFrame
            {
                EncryptedPayload = encrypted,
                Hmac = hmac
            };

            var result = await dispatcher.DispatchSecureFrameAsync(frame, _aesKey, _hmacKey, CancellationToken.None);

            Assert.True(result.Success);
            _launcherMock.Verify(l => l.LaunchGameAsync("DOTA_2", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SecurityPipeline_InvalidHmac_ShouldBeRejectedAndLogAudit()
        {
            var dispatcher = CreateDispatcher(Array.Empty<IRemoteCommandHandler>());

            var frame = new SecureMessageFrame
            {
                EncryptedPayload = new byte[] { 1, 2, 3 },
                Hmac = new byte[32] // Invalid HMAC
            };

            var result = await dispatcher.DispatchSecureFrameAsync(frame, _aesKey, _hmacKey, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("INVALID_HMAC", result.ErrorCode);
            _auditLoggerMock.Verify(a => a.LogSecurity(It.Is<string>(s => s.Contains("Integrity check failed")), null), Times.Once);
        }

        [Fact]
        public async Task SecurityPipeline_InvalidSignature_ShouldBeRejectedAndLogAudit()
        {
            var handler = new LockPcCommandHandler(_powerMock.Object);
            var dispatcher = CreateDispatcher(new[] { handler });

            var command = new RemoteCommand
            {
                CommandId = Guid.NewGuid(),
                Action = "LOCK_PC",
                SenderAdminId = "Admin_01",
                Timestamp = DateTime.UtcNow,
                Signature = "INVALID_SIGNATURE_BASE64",
                Nonce = "nonce123",
                ExpirationTime = DateTime.UtcNow.AddMinutes(5)
            };

            var result = await dispatcher.DispatchAsync(command, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("INVALID_SIGNATURE", result.ErrorCode);
            _auditLoggerMock.Verify(a => a.LogSecurity(It.Is<string>(s => s.Contains("INVALID_SIGNATURE")), null), Times.Once);
        }

        [Fact]
        public async Task SecurityPipeline_ExpiredCommand_ShouldBeRejected()
        {
            var handler = new LockPcCommandHandler(_powerMock.Object);
            var dispatcher = CreateDispatcher(new[] { handler });

            var command = new RemoteCommand
            {
                CommandId = Guid.NewGuid(),
                Action = "LOCK_PC",
                SenderAdminId = "Admin_01",
                Timestamp = DateTime.UtcNow.AddMinutes(-10), // Skewed/expired timestamp
                Signature = "sig",
                Nonce = "nonce123",
                ExpirationTime = DateTime.UtcNow.AddMinutes(-1) // Already expired
            };

            var result = await dispatcher.DispatchAsync(command, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("EXPIRED", result.ErrorCode);
        }

        [Fact]
        public async Task SecurityPipeline_ReplayedNonce_ShouldBeRejected()
        {
            _powerMock.Setup(p => p.LockWorkstationAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var handler = new LockPcCommandHandler(_powerMock.Object);
            var dispatcher = CreateDispatcher(new[] { handler });

            var commandId1 = Guid.NewGuid();
            var commandId2 = Guid.NewGuid();
            var timestamp = DateTime.UtcNow;
            var sig1 = SignCommand(commandId1, "LOCK_PC", "Admin_01", timestamp, "", "nonce_dup");
            var sig2 = SignCommand(commandId2, "LOCK_PC", "Admin_01", timestamp, "", "nonce_dup");

            var command1 = new RemoteCommand
            {
                CommandId = commandId1,
                Action = "LOCK_PC",
                SenderAdminId = "Admin_01",
                Timestamp = timestamp,
                Signature = sig1,
                Nonce = "nonce_dup",
                ExpirationTime = DateTime.UtcNow.AddMinutes(5)
            };

            var command2 = new RemoteCommand
            {
                CommandId = commandId2,
                Action = "LOCK_PC",
                SenderAdminId = "Admin_01",
                Timestamp = timestamp,
                Signature = sig2,
                Nonce = "nonce_dup", // Reused nonce
                ExpirationTime = DateTime.UtcNow.AddMinutes(5)
            };

            var result1 = await dispatcher.DispatchAsync(command1, CancellationToken.None);
            var result2 = await dispatcher.DispatchAsync(command2, CancellationToken.None);

            Assert.True(result1.Success);
            Assert.False(result2.Success);
            Assert.Equal("REPLAY_ATTACK", result2.ErrorCode);
        }

        #endregion

        #region Engine Priority Queueing & Processing Tests

        [Fact]
        public async Task Engine_ShouldProcessCommandsInPriorityOrder()
        {
            var reporter = new CommandResultReporter(_reporterLoggerMock.Object, _auditLoggerMock.Object);
            var dispatcherMock = new Mock<IRemoteCommandDispatcher>();
            var processedActions = new List<string>();

            dispatcherMock.Setup(d => d.DispatchAsync(It.IsAny<RemoteCommand>(), It.IsAny<CancellationToken>()))
                .Callback<RemoteCommand, CancellationToken>((cmd, token) =>
                {
                    lock (processedActions)
                    {
                        processedActions.Add(cmd.Action);
                    }
                })
                .ReturnsAsync((RemoteCommand cmd, CancellationToken token) => CommandResult.Successful(cmd.CommandId));

            var engine = new RemoteCommandEngine(
                _engineLoggerMock.Object,
                _healthMonitorMock.Object,
                dispatcherMock.Object,
                reporter
            );

            var cmdLow = new RemoteCommand { CommandId = Guid.NewGuid(), Action = "LOW_PRIORITY", Priority = "Low" };
            var cmdNormal = new RemoteCommand { CommandId = Guid.NewGuid(), Action = "NORMAL_PRIORITY", Priority = "Normal" };
            var cmdHigh = new RemoteCommand { CommandId = Guid.NewGuid(), Action = "HIGH_PRIORITY", Priority = "High" };

            // Queue commands while engine is not running to verify priority sort
            await engine.QueueCommandAsync(cmdLow);
            await engine.QueueCommandAsync(cmdHigh);
            await engine.QueueCommandAsync(cmdNormal);

            // Run the engine loop under cancellation
            var cts = new CancellationTokenSource();
            var runTask = Task.Run(() => engine.RunSupervisedAsync(cts.Token));

            // Allow some execution time, then stop
            await Task.Delay(200);
            cts.Cancel();
            try { await runTask; } catch { }

            lock (processedActions)
            {
                Assert.Equal(3, processedActions.Count);
                Assert.Equal("HIGH_PRIORITY", processedActions[0]);
                Assert.Equal("NORMAL_PRIORITY", processedActions[1]);
                Assert.Equal("LOW_PRIORITY", processedActions[2]);
            }
        }

        [Fact]
        public async Task Engine_HandlerFailure_ShouldBeIsolatedAndReported()
        {
            var reporter = new CommandResultReporter(_reporterLoggerMock.Object, _auditLoggerMock.Object);
            var dispatcherMock = new Mock<IRemoteCommandDispatcher>();

            dispatcherMock.Setup(d => d.DispatchAsync(It.IsAny<RemoteCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Crashed on purpose"));

            var engine = new RemoteCommandEngine(
                _engineLoggerMock.Object,
                _healthMonitorMock.Object,
                dispatcherMock.Object,
                reporter
            );

            var cmd = new RemoteCommand { CommandId = Guid.NewGuid(), Action = "LOCK_PC", Priority = "High" };
            await engine.QueueCommandAsync(cmd);

            var cts = new CancellationTokenSource();
            var runTask = Task.Run(() => engine.RunSupervisedAsync(cts.Token));

            await Task.Delay(200);
            cts.Cancel();
            try { await runTask; } catch { }

            var status = await engine.GetCommandStatusAsync(cmd.CommandId);
            Assert.Equal(CommandStatus.Failed, status);
        }

        #endregion

        #region Native Windows Integration Placeholder NotImplemented Tests

        [Fact]
        public async Task WakeOnLanHandler_ShouldThrowNotImplementedException()
        {
            var handler = new WakeOnLanCommandHandler();
            var command = new RemoteCommand { CommandId = Guid.NewGuid(), Action = "WAKE_ON_LAN" };

            await Assert.ThrowsAsync<NotImplementedException>(() => handler.HandleAsync(command, CancellationToken.None));
        }

        [Fact]
        public async Task RestartApplicationHandler_ShouldThrowNotImplementedException()
        {
            var handler = new RestartApplicationCommandHandler();
            var command = new RemoteCommand { CommandId = Guid.NewGuid(), Action = "RESTART_APPLICATION" };

            await Assert.ThrowsAsync<NotImplementedException>(() => handler.HandleAsync(command, CancellationToken.None));
        }

        [Fact]
        public async Task RestartServiceHandler_ShouldThrowNotImplementedException()
        {
            var handler = new RestartServiceCommandHandler();
            var command = new RemoteCommand { CommandId = Guid.NewGuid(), Action = "RESTART_SERVICE" };

            await Assert.ThrowsAsync<NotImplementedException>(() => handler.HandleAsync(command, CancellationToken.None));
        }

        #endregion
    }
}
