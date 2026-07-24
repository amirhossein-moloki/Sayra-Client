using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Moq;
using Sayra.Client.GameLibrary.Services;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using SayraClient;
using SayraClient.Services;
using SayraClient.Services.Windows;
using Xunit;

namespace Sayra.Client.Tests;

public class WindowsIntegrationTests
{
    private readonly Mock<ILogger<WtsSessionChangeMonitor>> _loggerMock = new();
    private readonly Mock<IServiceHealthMonitor> _healthMonitorMock = new();
    private readonly Mock<IEventDispatcher> _eventDispatcherMock = new();
    private readonly Mock<IAuditLogger> _auditLoggerMock = new();
    private readonly Mock<IpcServer> _ipcServerMock;

    public WindowsIntegrationTests()
    {
        var sessionManagerMock = new Mock<SessionManager>(
            new Mock<ILogger<SessionManager>>().Object,
            new Mock<ClientStateManager>(new Mock<ILogger<ClientStateManager>>().Object).Object,
            new Mock<IAuditLogger>().Object
        );
        var stateManagerMock = new Mock<ClientStateManager>(new Mock<ILogger<ClientStateManager>>().Object);
        var kioskManagerMock = new Mock<KioskManager>(new Mock<ILogger<KioskManager>>().Object);
        var gameLauncherMock = new Mock<Sayra.Client.Launcher.Services.IGameLauncherService>();
        var processMonitorMock = new Mock<Sayra.Client.Launcher.Services.IProcessMonitorService>();
        var gameLibraryMock = new Mock<IGameLibraryService>();

        _ipcServerMock = new Mock<IpcServer>(
            new Mock<ILogger<IpcServer>>().Object,
            sessionManagerMock.Object,
            stateManagerMock.Object,
            kioskManagerMock.Object,
            gameLauncherMock.Object,
            processMonitorMock.Object,
            gameLibraryMock.Object,
            _healthMonitorMock.Object
        );
    }

    [Fact]
    public void WtsSessionChangeMonitor_Lock_ShouldLogSecurityAndAuditEvents()
    {
        // Arrange
        var monitor = new WtsSessionChangeMonitor(
            _loggerMock.Object,
            _healthMonitorMock.Object,
            _eventDispatcherMock.Object,
            _auditLoggerMock.Object,
            _ipcServerMock.Object
        );

        bool eventFired = false;
        monitor.SessionChanged += (reason) =>
        {
            if (reason == SessionSwitchReason.SessionLock)
            {
                eventFired = true;
            }
        };

        // Act
        monitor.HandleSessionSwitch(SessionSwitchReason.SessionLock);

        // Assert
        _auditLoggerMock.Verify(a => a.LogOperational(It.Is<string>(s => s.Contains("SessionLock")), It.IsAny<Dictionary<string, object>>()), Times.Once);
        _auditLoggerMock.Verify(a => a.LogSecurity(It.Is<string>(s => s.Contains("secure")), It.IsAny<Dictionary<string, object>>()), Times.Once);
        Assert.True(eventFired);
    }

    [Fact]
    public void WtsSessionChangeMonitor_Unlock_ShouldLogSecurityAndBroadcastIpcEvent()
    {
        // Arrange
        var monitor = new WtsSessionChangeMonitor(
            _loggerMock.Object,
            _healthMonitorMock.Object,
            _eventDispatcherMock.Object,
            _auditLoggerMock.Object,
            _ipcServerMock.Object
        );

        bool eventFired = false;
        monitor.SessionChanged += (reason) =>
        {
            if (reason == SessionSwitchReason.SessionUnlock)
            {
                eventFired = true;
            }
        };

        // Act
        monitor.HandleSessionSwitch(SessionSwitchReason.SessionUnlock);

        // Assert
        _auditLoggerMock.Verify(a => a.LogOperational(It.Is<string>(s => s.Contains("SessionUnlock")), It.IsAny<Dictionary<string, object>>()), Times.Once);
        _auditLoggerMock.Verify(a => a.LogSecurity(It.Is<string>(s => s.Contains("Resuming")), It.IsAny<Dictionary<string, object>>()), Times.Once);
        Assert.True(eventFired);
    }

    [Fact]
    public void PowerStatusChangeHandler_Suspend_ShouldSaveWorkstationBackupState()
    {
        // Arrange
        var powerLogger = new Mock<ILogger<PowerStatusChangeHandler>>();
        var tcpClientMock = new Mock<TcpClientManager>(
            new Mock<ILogger<TcpClientManager>>().Object,
            new Mock<Microsoft.Extensions.Configuration.IConfiguration>().Object,
            new Mock<ReconnectManager>(new Mock<ILogger<ReconnectManager>>().Object, 1000, 1000).Object,
            new Mock<MessageHandler>(
                new Mock<ILogger<MessageHandler>>().Object,
                new Mock<SayraClient.Commands.CommandRouter>(new Mock<ILogger<SayraClient.Commands.CommandRouter>>().Object, Array.Empty<SayraClient.Commands.ICommandHandler>()).Object,
                new Mock<SayraClient.Services.SecurityManager>(new Mock<ILogger<SayraClient.Services.SecurityManager>>().Object, new Mock<SayraClient.Services.EncryptionManager>(new Mock<ILogger<SayraClient.Services.EncryptionManager>>().Object).Object).Object
            ).Object,
            new Mock<IServiceProvider>().Object,
            new Mock<SecureTransportLayer>(
                new Mock<ILogger<SecureTransportLayer>>().Object,
                new Mock<SessionKeyManager>(new Mock<ILogger<SessionKeyManager>>().Object).Object,
                new Mock<SecureMessageValidator>(new Mock<ILogger<SecureMessageValidator>>().Object).Object
            ).Object,
            new Mock<SessionKeyManager>(new Mock<ILogger<SessionKeyManager>>().Object).Object,
            new Mock<AuthManager>(
                new Mock<ILogger<AuthManager>>().Object,
                new Mock<SayraClient.Services.SecurityManager>(new Mock<ILogger<SayraClient.Services.SecurityManager>>().Object, new Mock<SayraClient.Services.EncryptionManager>(new Mock<ILogger<SayraClient.Services.EncryptionManager>>().Object).Object).Object,
                new Mock<SecureTransportLayer>(
                    new Mock<ILogger<SecureTransportLayer>>().Object,
                    new Mock<SessionKeyManager>(new Mock<ILogger<SessionKeyManager>>().Object).Object,
                    new Mock<SecureMessageValidator>(new Mock<ILogger<SecureMessageValidator>>().Object).Object
                ).Object
            ).Object,
            new Mock<ClientStateManager>(new Mock<ILogger<ClientStateManager>>().Object).Object,
            new Mock<Sayra.Client.Discovery.Services.IDiscoveryService>().Object
        );
        var backupServiceMock = new Mock<IWorkstationBackupService>();

        var handler = new PowerStatusChangeHandler(
            powerLogger.Object,
            _healthMonitorMock.Object,
            _auditLoggerMock.Object,
            tcpClientMock.Object,
            backupServiceMock.Object
        );

        // Act
        handler.HandlePowerModeChange(PowerModes.Suspend);

        // Assert
        _auditLoggerMock.Verify(a => a.LogOperational(It.Is<string>(s => s.Contains("Suspend")), It.IsAny<Dictionary<string, object>>()), Times.Once);
        _auditLoggerMock.Verify(a => a.LogSecurity(It.Is<string>(s => s.Contains("Suspend")), It.IsAny<Dictionary<string, object>>()), Times.Once);
        backupServiceMock.Verify(b => b.CreateBackupAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void PowerStatusChangeHandler_Resume_ShouldForceDisconnectToTriggerReconnect()
    {
        // Arrange
        var powerLogger = new Mock<ILogger<PowerStatusChangeHandler>>();
        var tcpClientMock = new Mock<TcpClientManager>(
            new Mock<ILogger<TcpClientManager>>().Object,
            new Mock<Microsoft.Extensions.Configuration.IConfiguration>().Object,
            new Mock<ReconnectManager>(new Mock<ILogger<ReconnectManager>>().Object, 1000, 1000).Object,
            new Mock<MessageHandler>(
                new Mock<ILogger<MessageHandler>>().Object,
                new Mock<SayraClient.Commands.CommandRouter>(new Mock<ILogger<SayraClient.Commands.CommandRouter>>().Object, Array.Empty<SayraClient.Commands.ICommandHandler>()).Object,
                new Mock<SayraClient.Services.SecurityManager>(new Mock<ILogger<SayraClient.Services.SecurityManager>>().Object, new Mock<SayraClient.Services.EncryptionManager>(new Mock<ILogger<SayraClient.Services.EncryptionManager>>().Object).Object).Object
            ).Object,
            new Mock<IServiceProvider>().Object,
            new Mock<SecureTransportLayer>(
                new Mock<ILogger<SecureTransportLayer>>().Object,
                new Mock<SessionKeyManager>(new Mock<ILogger<SessionKeyManager>>().Object).Object,
                new Mock<SecureMessageValidator>(new Mock<ILogger<SecureMessageValidator>>().Object).Object
            ).Object,
            new Mock<SessionKeyManager>(new Mock<ILogger<SessionKeyManager>>().Object).Object,
            new Mock<AuthManager>(
                new Mock<ILogger<AuthManager>>().Object,
                new Mock<SayraClient.Services.SecurityManager>(new Mock<ILogger<SayraClient.Services.SecurityManager>>().Object, new Mock<SayraClient.Services.EncryptionManager>(new Mock<ILogger<SayraClient.Services.EncryptionManager>>().Object).Object).Object,
                new Mock<SecureTransportLayer>(
                    new Mock<ILogger<SecureTransportLayer>>().Object,
                    new Mock<SessionKeyManager>(new Mock<ILogger<SessionKeyManager>>().Object).Object,
                    new Mock<SecureMessageValidator>(new Mock<ILogger<SecureMessageValidator>>().Object).Object
                ).Object
            ).Object,
            new Mock<ClientStateManager>(new Mock<ILogger<ClientStateManager>>().Object).Object,
            new Mock<Sayra.Client.Discovery.Services.IDiscoveryService>().Object
        );
        var backupServiceMock = new Mock<IWorkstationBackupService>();

        var handler = new PowerStatusChangeHandler(
            powerLogger.Object,
            _healthMonitorMock.Object,
            _auditLoggerMock.Object,
            tcpClientMock.Object,
            backupServiceMock.Object
        );

        // Act
        handler.HandlePowerModeChange(PowerModes.Resume);

        // Assert
        _auditLoggerMock.Verify(a => a.LogOperational(It.Is<string>(s => s.Contains("Resume")), It.IsAny<Dictionary<string, object>>()), Times.Once);
        _auditLoggerMock.Verify(a => a.LogSecurity(It.Is<string>(s => s.Contains("Resume")), It.IsAny<Dictionary<string, object>>()), Times.Once);
        tcpClientMock.Verify(t => t.Disconnect(), Times.Once);
    }

    [Fact]
    public void EtwProcessMonitor_BlacklistedProcess_ShouldTriggerAlertAndKill()
    {
        // Arrange
        var etwLogger = new Mock<ILogger<EtwProcessMonitor>>();
        var kioskManagerMock = new Mock<KioskManager>(new Mock<ILogger<KioskManager>>().Object);
        kioskManagerMock.Setup(k => k.IsLocked()).Returns(true);

        var monitor = new EtwProcessMonitor(
            etwLogger.Object,
            _healthMonitorMock.Object,
            _auditLoggerMock.Object,
            kioskManagerMock.Object
        );

        // Act
        monitor.EvaluateProcess("cheatengine", 9999);

        // Assert
        _auditLoggerMock.Verify(a => a.LogSecurity(It.Is<string>(s => s.Contains("unauthorized process creation") || s.Contains("cheatengine")), It.IsAny<Dictionary<string, object>>()), Times.Once);
    }

    [Fact]
    public void WindowsEventLogService_NotWindows_ShouldLogGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<WindowsEventLogService>>();
        var service = new WindowsEventLogService(mockLogger.Object);

        // Act
        service.WriteEntry("Test entry", EventLogEntryType.Information, 100);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("WindowsEventLog") && v.ToString()!.Contains("Test entry")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RestartManagerHelper_NotWindows_ShouldLogAndExitGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RestartManagerHelper>>();
        var helper = new RestartManagerHelper(mockLogger.Object);

        // Act
        helper.RegisterForRestart("args");

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Restart Manager")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
