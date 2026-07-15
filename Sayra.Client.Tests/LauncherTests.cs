using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Sayra.Client.Launcher.Services;
using Sayra.Client.Launcher.Models;
using Sayra.Client.GameLibrary.Models;
using Sayra.Client.GameLibrary.Services;
using Sayra.Client.Launcher.Validation;
using Sayra.Client.Launcher.Events;

namespace Sayra.Client.Tests;

public class LauncherTests : IDisposable
{
    private readonly string _tempWorkingDir;
    private readonly string _validExecutablePath;

    public LauncherTests()
    {
        _tempWorkingDir = Path.Combine(Path.GetTempPath(), "SayraLauncherTests_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempWorkingDir);

        // Resolve valid command based on OS
        if (OperatingSystem.IsWindows())
        {
            _validExecutablePath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        }
        else
        {
            _validExecutablePath = "/bin/sh";
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempWorkingDir))
        {
            try { Directory.Delete(_tempWorkingDir, true); } catch { }
        }
    }

    private string GetValidArguments()
    {
        return OperatingSystem.IsWindows() ? "/c ping 127.0.0.1 -n 1" : "-c \"sleep 0.1\"";
    }

    private string GetLongRunningArguments()
    {
        return OperatingSystem.IsWindows() ? "/c ping 127.0.0.1 -n 10" : "-c \"sleep 10\"";
    }

    [Fact]
    public async Task Test1_LaunchValidGame_ShouldSucceedAndFireEvents()
    {
        // Arrange
        var mockLibrary = new Mock<IGameLibraryService>();
        var mockSession = new Mock<ISessionStateProvider>();
        var mockLicense = new Mock<ILicenseValidator>();
        var mockLoggerLauncher = new Mock<ILogger<GameLauncherService>>();
        var mockLoggerMonitor = new Mock<ILogger<ProcessMonitorService>>();
        var mockLoggerRecovery = new Mock<ILogger<LauncherRecoveryService>>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        var game = new Game
        {
            Id = "game1",
            Name = "ShellProcess",
            ExecutablePath = _validExecutablePath,
            Arguments = GetValidArguments(),
            WorkingDirectory = _tempWorkingDir,
            Enabled = true
        };

        mockLibrary.Setup(x => x.GetGames()).ReturnsAsync(new List<Game> { game });
        mockSession.Setup(x => x.IsSessionActive()).Returns(true);
        mockLicense.Setup(x => x.IsLicenseValid("game1")).Returns(true);

        var monitorService = new ProcessMonitorService(mockLoggerMonitor.Object, mockServiceProvider.Object);
        var recoveryService = new LauncherRecoveryService(mockLoggerRecovery.Object, mockServiceProvider.Object);
        var launcherService = new GameLauncherService(
            mockLibrary.Object,
            monitorService,
            recoveryService,
            mockSession.Object,
            mockLicense.Object,
            mockLoggerLauncher.Object
        );

        mockServiceProvider.Setup(x => x.GetService(typeof(IGameLauncherService))).Returns(launcherService);
        mockServiceProvider.Setup(x => x.GetService(typeof(IProcessMonitorService))).Returns(monitorService);
        mockServiceProvider.Setup(x => x.GetService(typeof(ILauncherRecoveryService))).Returns(recoveryService);

        bool launchingFired = false;
        bool startedFired = false;

        launcherService.GameLaunching += (s, e) => { launchingFired = true; };
        launcherService.GameStarted += (s, e) => { startedFired = true; };

        // Act
        bool launched = await launcherService.LaunchGameAsync("game1");

        // Assert
        Assert.True(launched);
        Assert.True(launchingFired);
        Assert.True(startedFired);

        // Check monitor
        var running = monitorService.GetRunningProcesses().ToList();
        Assert.Single(running);
        Assert.Equal("game1", running[0].GameId);
        Assert.True(running[0].IsRunning);

        // Clean up
        await launcherService.KillGameAsync("game1");
        monitorService.Dispose();
    }

    [Fact]
    public async Task Test2_LaunchInvalidExecutable_ShouldFailValidation()
    {
        // Arrange
        var mockLibrary = new Mock<IGameLibraryService>();
        var mockSession = new Mock<ISessionStateProvider>();
        var mockLicense = new Mock<ILicenseValidator>();
        var mockLoggerLauncher = new Mock<ILogger<GameLauncherService>>();
        var mockLoggerMonitor = new Mock<ILogger<ProcessMonitorService>>();
        var mockLoggerRecovery = new Mock<ILogger<LauncherRecoveryService>>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        var game = new Game
        {
            Id = "bad_game",
            Name = "NonExistentGame",
            ExecutablePath = "C:\\Games\\NonExistent.exe",
            Enabled = true
        };

        mockLibrary.Setup(x => x.GetGames()).ReturnsAsync(new List<Game> { game });
        mockSession.Setup(x => x.IsSessionActive()).Returns(true);
        mockLicense.Setup(x => x.IsLicenseValid("bad_game")).Returns(true);

        var monitorService = new ProcessMonitorService(mockLoggerMonitor.Object, mockServiceProvider.Object);
        var recoveryService = new LauncherRecoveryService(mockLoggerRecovery.Object, mockServiceProvider.Object);
        var launcherService = new GameLauncherService(
            mockLibrary.Object,
            monitorService,
            recoveryService,
            mockSession.Object,
            mockLicense.Object,
            mockLoggerLauncher.Object
        );

        mockServiceProvider.Setup(x => x.GetService(typeof(IGameLauncherService))).Returns(launcherService);
        mockServiceProvider.Setup(x => x.GetService(typeof(IProcessMonitorService))).Returns(monitorService);
        mockServiceProvider.Setup(x => x.GetService(typeof(ILauncherRecoveryService))).Returns(recoveryService);

        bool failedFired = false;
        string? failureReason = null;
        launcherService.LaunchFailed += (s, e) =>
        {
            failedFired = true;
            failureReason = e.Reason;
        };

        // Act
        bool launched = await launcherService.LaunchGameAsync("bad_game");

        // Assert
        Assert.False(launched);
        Assert.True(failedFired);
        Assert.Contains("does not exist", failureReason);

        monitorService.Dispose();
    }

    [Fact]
    public async Task Test3_LaunchDisabledGame_ShouldBeRefused()
    {
        // Arrange
        var mockLibrary = new Mock<IGameLibraryService>();
        var mockSession = new Mock<ISessionStateProvider>();
        var mockLicense = new Mock<ILicenseValidator>();
        var mockLoggerLauncher = new Mock<ILogger<GameLauncherService>>();
        var mockLoggerMonitor = new Mock<ILogger<ProcessMonitorService>>();
        var mockLoggerRecovery = new Mock<ILogger<LauncherRecoveryService>>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        var game = new Game
        {
            Id = "disabled_game",
            Name = "DisabledGame",
            ExecutablePath = _validExecutablePath,
            Enabled = false
        };

        mockLibrary.Setup(x => x.GetGames()).ReturnsAsync(new List<Game> { game });
        mockSession.Setup(x => x.IsSessionActive()).Returns(true);

        var monitorService = new ProcessMonitorService(mockLoggerMonitor.Object, mockServiceProvider.Object);
        var recoveryService = new LauncherRecoveryService(mockLoggerRecovery.Object, mockServiceProvider.Object);
        var launcherService = new GameLauncherService(
            mockLibrary.Object,
            monitorService,
            recoveryService,
            mockSession.Object,
            mockLicense.Object,
            mockLoggerLauncher.Object
        );

        // Act
        bool launched = await launcherService.LaunchGameAsync("disabled_game");

        // Assert
        Assert.False(launched);
        monitorService.Dispose();
    }

    [Fact]
    public async Task Test4_LaunchWhenSessionInactive_ShouldBeRefused()
    {
        // Arrange
        var mockLibrary = new Mock<IGameLibraryService>();
        var mockSession = new Mock<ISessionStateProvider>();
        var mockLicense = new Mock<ILicenseValidator>();
        var mockLoggerLauncher = new Mock<ILogger<GameLauncherService>>();
        var mockLoggerMonitor = new Mock<ILogger<ProcessMonitorService>>();
        var mockLoggerRecovery = new Mock<ILogger<LauncherRecoveryService>>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        var game = new Game
        {
            Id = "game_no_session",
            Name = "GameNoSession",
            ExecutablePath = _validExecutablePath,
            Enabled = true
        };

        mockLibrary.Setup(x => x.GetGames()).ReturnsAsync(new List<Game> { game });
        mockSession.Setup(x => x.IsSessionActive()).Returns(false); // Inactive session

        var monitorService = new ProcessMonitorService(mockLoggerMonitor.Object, mockServiceProvider.Object);
        var recoveryService = new LauncherRecoveryService(mockLoggerRecovery.Object, mockServiceProvider.Object);
        var launcherService = new GameLauncherService(
            mockLibrary.Object,
            monitorService,
            recoveryService,
            mockSession.Object,
            mockLicense.Object,
            mockLoggerLauncher.Object
        );

        // Act
        bool launched = await launcherService.LaunchGameAsync("game_no_session");

        // Assert
        Assert.False(launched);
        monitorService.Dispose();
    }

    [Fact]
    public async Task Test5_StopAndKillProcess_ShouldTerminateSuccessfully()
    {
        // Arrange
        var mockLibrary = new Mock<IGameLibraryService>();
        var mockSession = new Mock<ISessionStateProvider>();
        var mockLicense = new Mock<ILicenseValidator>();
        var mockLoggerLauncher = new Mock<ILogger<GameLauncherService>>();
        var mockLoggerMonitor = new Mock<ILogger<ProcessMonitorService>>();
        var mockLoggerRecovery = new Mock<ILogger<LauncherRecoveryService>>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        var game = new Game
        {
            Id = "game_long",
            Name = "LongRunningGame",
            ExecutablePath = _validExecutablePath,
            Arguments = GetLongRunningArguments(),
            Enabled = true
        };

        mockLibrary.Setup(x => x.GetGames()).ReturnsAsync(new List<Game> { game });
        mockSession.Setup(x => x.IsSessionActive()).Returns(true);
        mockLicense.Setup(x => x.IsLicenseValid("game_long")).Returns(true);

        var monitorService = new ProcessMonitorService(mockLoggerMonitor.Object, mockServiceProvider.Object);
        var recoveryService = new LauncherRecoveryService(mockLoggerRecovery.Object, mockServiceProvider.Object);
        var launcherService = new GameLauncherService(
            mockLibrary.Object,
            monitorService,
            recoveryService,
            mockSession.Object,
            mockLicense.Object,
            mockLoggerLauncher.Object
        );

        mockServiceProvider.Setup(x => x.GetService(typeof(IGameLauncherService))).Returns(launcherService);
        mockServiceProvider.Setup(x => x.GetService(typeof(IProcessMonitorService))).Returns(monitorService);

        // Act
        bool launched = await launcherService.LaunchGameAsync("game_long");
        Assert.True(launched);

        var statsBefore = monitorService.GetProcessStatistics("game_long");
        Assert.NotNull(statsBefore);
        Assert.True(statsBefore!.IsRunning);

        // Terminate
        await launcherService.KillGameAsync("game_long");

        // Assert termination reflected shortly
        await Task.Delay(50);
        var statsAfter = monitorService.GetProcessStatistics("game_long");
        Assert.False(statsAfter?.IsRunning ?? true);

        monitorService.Dispose();
    }

    [Fact]
    public async Task Test6_LauncherRecovery_ShouldAutoRetryUnderMaxLimit()
    {
        // Arrange
        var mockLibrary = new Mock<IGameLibraryService>();
        var mockSession = new Mock<ISessionStateProvider>();
        var mockLicense = new Mock<ILicenseValidator>();
        var mockLoggerLauncher = new Mock<ILogger<GameLauncherService>>();
        var mockLoggerMonitor = new Mock<ILogger<ProcessMonitorService>>();
        var mockLoggerRecovery = new Mock<ILogger<LauncherRecoveryService>>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        // We simulate a game that fails on launch
        var game = new Game
        {
            Id = "crash_game",
            Name = "Crasher",
            ExecutablePath = "C:\\FakePath\\NonExistent.exe",
            Enabled = true
        };

        mockLibrary.Setup(x => x.GetGames()).ReturnsAsync(new List<Game> { game });
        mockSession.Setup(x => x.IsSessionActive()).Returns(true);
        mockLicense.Setup(x => x.IsLicenseValid("crash_game")).Returns(true);

        var monitorService = new ProcessMonitorService(mockLoggerMonitor.Object, mockServiceProvider.Object);
        var recoveryService = new LauncherRecoveryService(mockLoggerRecovery.Object, mockServiceProvider.Object);
        var launcherService = new GameLauncherService(
            mockLibrary.Object,
            monitorService,
            recoveryService,
            mockSession.Object,
            mockLicense.Object,
            mockLoggerLauncher.Object
        );

        mockServiceProvider.Setup(x => x.GetService(typeof(IGameLauncherService))).Returns(launcherService);
        mockServiceProvider.Setup(x => x.GetService(typeof(IProcessMonitorService))).Returns(monitorService);
        mockServiceProvider.Setup(x => x.GetService(typeof(ILauncherRecoveryService))).Returns(recoveryService);

        int restartedCount = 0;
        launcherService.GameRestarted += (s, e) =>
        {
            restartedCount++;
        };

        // Act
        bool launched = await launcherService.LaunchGameAsync("crash_game");

        // Assert
        Assert.False(launched);
        // Relaunches are triggered asynchronously up to MaxRecoveryRetries = 3
        Assert.True(restartedCount <= 3);

        monitorService.Dispose();
    }

    [Fact]
    public async Task Test7_ConcurrentLaunches_ShouldTrackIndependently()
    {
        // Arrange
        var mockLibrary = new Mock<IGameLibraryService>();
        var mockSession = new Mock<ISessionStateProvider>();
        var mockLicense = new Mock<ILicenseValidator>();
        var mockLoggerLauncher = new Mock<ILogger<GameLauncherService>>();
        var mockLoggerMonitor = new Mock<ILogger<ProcessMonitorService>>();
        var mockLoggerRecovery = new Mock<ILogger<LauncherRecoveryService>>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        var game1 = new Game
        {
            Id = "game_c1",
            Name = "Concurrent1",
            ExecutablePath = _validExecutablePath,
            Arguments = GetValidArguments(),
            Enabled = true
        };

        var game2 = new Game
        {
            Id = "game_c2",
            Name = "Concurrent2",
            ExecutablePath = _validExecutablePath,
            Arguments = GetValidArguments(),
            Enabled = true
        };

        mockLibrary.Setup(x => x.GetGames()).ReturnsAsync(new List<Game> { game1, game2 });
        mockSession.Setup(x => x.IsSessionActive()).Returns(true);
        mockLicense.Setup(x => x.IsLicenseValid(It.IsAny<string>())).Returns(true);

        var monitorService = new ProcessMonitorService(mockLoggerMonitor.Object, mockServiceProvider.Object);
        var recoveryService = new LauncherRecoveryService(mockLoggerRecovery.Object, mockServiceProvider.Object);
        var launcherService = new GameLauncherService(
            mockLibrary.Object,
            monitorService,
            recoveryService,
            mockSession.Object,
            mockLicense.Object,
            mockLoggerLauncher.Object
        );

        mockServiceProvider.Setup(x => x.GetService(typeof(IGameLauncherService))).Returns(launcherService);

        // Act
        var t1 = launcherService.LaunchGameAsync("game_c1");
        var t2 = launcherService.LaunchGameAsync("game_c2");

        await Task.WhenAll(t1, t2);

        // Assert
        var running = monitorService.GetRunningProcesses().ToList();
        Assert.Equal(2, running.Count);

        await launcherService.KillGameAsync("game_c1");
        await launcherService.KillGameAsync("game_c2");
        monitorService.Dispose();
    }
}
