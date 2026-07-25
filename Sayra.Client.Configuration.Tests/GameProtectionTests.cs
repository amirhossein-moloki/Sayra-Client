using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;
using Sayra.Client.Shared.Security.GameProtection.Application.Services;
using Sayra.Client.Shared.Security.GameProtection.Domain.Events;
using Sayra.Client.Shared.Security.GameProtection.Domain.Models;
using Sayra.Client.Shared.Security.GameProtection.Domain.Rules;
using Sayra.Client.Shared.Security.GameProtection.Infrastructure.Validators;
using Xunit;

namespace Sayra.Client.Configuration.Tests;

public class GameProtectionTests
{
    private readonly Mock<ILogger<ProcessPolicyEvaluator>> _mockEvaluatorLogger;
    private readonly Mock<ILogger<ThreatReporter>> _mockReporterLogger;
    private readonly Mock<ILogger<ProcessSecurityMonitor>> _mockMonitorLogger;
    private readonly Mock<ILogger<GameIntegrityValidator>> _mockIntegrityLogger;
    private readonly Mock<IAuditLogger> _mockAuditLogger;
    private readonly Mock<IEventDispatcher> _mockEventDispatcher;

    public GameProtectionTests()
    {
        _mockEvaluatorLogger = new Mock<ILogger<ProcessPolicyEvaluator>>();
        _mockReporterLogger = new Mock<ILogger<ThreatReporter>>();
        _mockMonitorLogger = new Mock<ILogger<ProcessSecurityMonitor>>();
        _mockIntegrityLogger = new Mock<ILogger<GameIntegrityValidator>>();
        _mockAuditLogger = new Mock<IAuditLogger>();
        _mockEventDispatcher = new Mock<IEventDispatcher>();
    }

    [Fact]
    public void PolicyEvaluation_AllowedProcess_ReturnsAllow()
    {
        // Arrange
        var policy = new ProcessPolicy
        {
            AllowedGames = new()
            {
                new AllowedGame { GameId = "game_1", ExecutableName = "game.exe", IsEnabled = true }
            }
        };

        var mockIntegrity = new Mock<IIntegrityValidator>();
        var evaluator = new ProcessPolicyEvaluator(policy, mockIntegrity.Object);
        var process = new ProcessInfo { ProcessId = 123, ProcessName = "game.exe", ExecutablePath = "C:\\Games\\game.exe" };

        // Act
        var decision = evaluator.Evaluate(process);

        // Assert
        Assert.Equal(ProcessAction.Allow, decision.Action);
    }

    [Fact]
    public void PolicyEvaluation_BlockedProcess_ReturnsTerminate()
    {
        // Arrange
        var policy = new ProcessPolicy
        {
            BlockedApplications = new()
            {
                new BlockedApplication { Name = "cheatengine.exe", Reason = "Cheating utility", Severity = "Critical" }
            }
        };

        var mockIntegrity = new Mock<IIntegrityValidator>();
        var evaluator = new ProcessPolicyEvaluator(policy, mockIntegrity.Object);
        var process = new ProcessInfo { ProcessId = 123, ProcessName = "cheatengine.exe", ExecutablePath = "C:\\Tools\\cheatengine.exe" };

        // Act
        var decision = evaluator.Evaluate(process);

        // Assert
        Assert.Equal(ProcessAction.Terminate, decision.Action);
        Assert.Contains("Blocked application", decision.Reason);
        Assert.Equal("Critical", decision.Severity);
    }

    [Fact]
    public void PolicyEvaluation_UnknownProcess_WithoutStrictWhitelisting_ReturnsAllow()
    {
        // Arrange
        var policy = new ProcessPolicy { StrictWhitelistingEnabled = false };
        var mockIntegrity = new Mock<IIntegrityValidator>();
        var evaluator = new ProcessPolicyEvaluator(policy, mockIntegrity.Object);
        var process = new ProcessInfo { ProcessId = 456, ProcessName = "unknown.exe", ExecutablePath = "C:\\unknown.exe" };

        // Act
        var decision = evaluator.Evaluate(process);

        // Assert
        Assert.Equal(ProcessAction.Allow, decision.Action);
    }

    [Fact]
    public void PolicyEvaluation_UnknownProcess_WithStrictWhitelisting_ReturnsTerminate()
    {
        // Arrange
        var policy = new ProcessPolicy { StrictWhitelistingEnabled = true };
        var mockIntegrity = new Mock<IIntegrityValidator>();
        var evaluator = new ProcessPolicyEvaluator(policy, mockIntegrity.Object);
        var process = new ProcessInfo { ProcessId = 456, ProcessName = "unknown.exe", ExecutablePath = "C:\\unknown.exe" };

        // Act
        var decision = evaluator.Evaluate(process);

        // Assert
        Assert.Equal(ProcessAction.Terminate, decision.Action);
    }

    [Fact]
    public void PolicyEvaluation_WhitelistedGame_FailedIntegrity_ReturnsTerminate()
    {
        // Arrange
        var policy = new ProcessPolicy
        {
            AllowedGames = new()
            {
                new AllowedGame
                {
                    GameId = "game_1",
                    ExecutableName = "game.exe",
                    ExpectedHash = "123456abcdef",
                    IsEnabled = true
                }
            }
        };

        var mockIntegrity = new Mock<IIntegrityValidator>();
        mockIntegrity.Setup(i => i.ValidateExecutable(It.IsAny<string>(), "123456abcdef", ""))
            .Returns(new IntegrityResult { Status = IntegrityStatus.Invalid, Reason = "Hash mismatch" });

        var evaluator = new ProcessPolicyEvaluator(policy, mockIntegrity.Object);
        var process = new ProcessInfo { ProcessId = 123, ProcessName = "game.exe", ExecutablePath = "C:\\Games\\game.exe" };

        // Act
        var decision = evaluator.Evaluate(process);

        // Assert
        Assert.Equal(ProcessAction.Terminate, decision.Action);
        Assert.Contains("failed integrity verification", decision.Reason);
    }

    [Fact]
    public void IntegrityValidation_ValidHash_ReturnsValid()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "SAYRA Game Protection Test Data");

        string expectedHash;
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            var bytes = File.ReadAllBytes(tempFile);
            var hashBytes = sha.ComputeHash(bytes);
            expectedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        var validator = new GameIntegrityValidator(_mockIntegrityLogger.Object);

        try
        {
            // Act
            var result = validator.ValidateExecutable(tempFile, expectedHash);

            // Assert
            Assert.Equal(IntegrityStatus.Valid, result.Status);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void IntegrityValidation_InvalidHash_ReturnsInvalid()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "SAYRA Game Protection Test Data");

        var validator = new GameIntegrityValidator(_mockIntegrityLogger.Object);

        try
        {
            // Act
            var result = validator.ValidateExecutable(tempFile, "wronghashvalue123");

            // Assert
            Assert.Equal(IntegrityStatus.Invalid, result.Status);
            Assert.Contains("SHA256 hash mismatch", result.Reason);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void IntegrityValidation_MissingFile_ReturnsInvalid()
    {
        // Arrange
        var validator = new GameIntegrityValidator(_mockIntegrityLogger.Object);
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");

        // Act
        var result = validator.ValidateExecutable(nonExistentPath, "hash");

        // Assert
        Assert.Equal(IntegrityStatus.Invalid, result.Status);
        Assert.Contains("does not exist on disk", result.Reason);
    }

    [Fact]
    public void RuleEngine_MultipleRules_HandlesPriorityAndSeverity()
    {
        // Arrange
        var policy = new ProcessPolicy
        {
            Rules = new()
            {
                new ProcessRule { ProcessName = "bad.exe", Action = ProcessAction.Terminate, Severity = "Critical" },
                new ProcessRule { ProcessName = "bad.exe", Action = ProcessAction.Report, Severity = "Low" }
            }
        };

        var mockIntegrity = new Mock<IIntegrityValidator>();
        var evaluator = new ProcessPolicyEvaluator(policy, mockIntegrity.Object);
        var process = new ProcessInfo { ProcessId = 789, ProcessName = "bad.exe", ExecutablePath = "C:\\bad.exe" };

        // Act
        var decision = evaluator.Evaluate(process);

        // Assert
        // Higher restriction action (Terminate) must take priority over Report
        Assert.Equal(ProcessAction.Terminate, decision.Action);
        Assert.Equal("Critical", decision.Severity);
    }

    [Fact]
    public void SecurityEvents_EventCreation_SetsPropertiesCorrectly()
    {
        // Act
        var timestamp = DateTime.UtcNow;
        var ev = new UnauthorizedProcessDetectedEvent
        {
            ProcessId = 999,
            ProcessName = "malicious.exe",
            Reason = "Not in whitelist",
            ExecutablePath = "C:\\malicious.exe",
            Timestamp = timestamp
        };

        // Assert
        Assert.Equal(999, ev.ProcessId);
        Assert.Equal("malicious.exe", ev.ProcessName);
        Assert.Equal("High", ev.Severity);
        Assert.Equal("Not in whitelist", ev.Reason);
        Assert.Equal("C:\\malicious.exe", ev.ExecutablePath);
        Assert.Equal(timestamp, ev.Timestamp);
    }

    [Fact]
    public void SecurityEvents_EventPublishing_CallsDispatcherAndAuditLogger()
    {
        // Arrange
        var reporter = new ThreatReporter(
            _mockReporterLogger.Object,
            _mockAuditLogger.Object,
            _mockEventDispatcher.Object);

        var ev = new UnauthorizedProcessDetectedEvent
        {
            ProcessId = 999,
            ProcessName = "malicious.exe",
            Reason = "Not in whitelist",
            ExecutablePath = "C:\\malicious.exe"
        };

        // Act
        reporter.ReportThreat(ev);

        // Assert
        _mockEventDispatcher.Verify(d => d.Dispatch<SecurityThreatEventBase>(ev), Times.Once);
        _mockAuditLogger.Verify(a => a.LogSecurity(
            It.Is<string>(s => s.Contains("Not in whitelist")),
            It.Is<System.Collections.Generic.Dictionary<string, object>>(d =>
                d.ContainsKey("ProcessName") && d["ProcessName"].ToString() == "malicious.exe")),
            Times.Once);
    }

    [Fact]
    public async Task ConfigFileTamperWatcher_DetectsModification_ReportsThreat()
    {
        // Arrange
        var mockReporter = new Mock<IThreatReporter>();
        var mockLogger = new Mock<ILogger<ConfigFileTamperWatcher>>();
        var watcher = new ConfigFileTamperWatcher(mockLogger.Object, mockReporter.Object);

        watcher.StartWatching();

        var testFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "client_config.json");

        try
        {
            // Act
            await File.WriteAllTextAsync(testFile, "{}");

            // Wait for FileSystemWatcher to raise the event
            await Task.Delay(1000);

            // Assert
            mockReporter.Verify(r => r.ReportThreat(It.Is<SecurityThreatEventBase>(ev =>
                ev is TamperingDetectedEvent && ev.Severity == "Critical")), Times.AtLeastOnce());
        }
        finally
        {
            watcher.Dispose();
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }
}
