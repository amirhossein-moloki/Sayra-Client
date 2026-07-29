using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Policies;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    public class ResilienceFoundationTests
    {
        #region 1. Enum Correctness Tests

        [Fact]
        public void Test_SubsystemHealthState_Enum_HasCorrectValues()
        {
            Assert.Equal(0, (int)SubsystemHealthState.Healthy);
            Assert.Equal(1, (int)SubsystemHealthState.Warning);
            Assert.Equal(2, (int)SubsystemHealthState.Critical);
            Assert.Equal(3, (int)SubsystemHealthState.Offline);
        }

        [Fact]
        public void Test_RecoveryStatus_Enum_HasCorrectValues()
        {
            Assert.Equal(RecoveryStatus.Pending, Enum.Parse<RecoveryStatus>("Pending"));
            Assert.Equal(RecoveryStatus.InProgress, Enum.Parse<RecoveryStatus>("InProgress"));
            Assert.Equal(RecoveryStatus.Success, Enum.Parse<RecoveryStatus>("Success"));
            Assert.Equal(RecoveryStatus.Failed, Enum.Parse<RecoveryStatus>("Failed"));
            Assert.Equal(RecoveryStatus.Cooldown, Enum.Parse<RecoveryStatus>("Cooldown"));
            Assert.Equal(RecoveryStatus.Cancelled, Enum.Parse<RecoveryStatus>("Cancelled"));
            Assert.Equal(RecoveryStatus.RetriesExceeded, Enum.Parse<RecoveryStatus>("RetriesExceeded"));
        }

        [Fact]
        public void Test_RecoveryPriority_Enum_HasCorrectValues()
        {
            Assert.Equal(RecoveryPriority.Low, Enum.Parse<RecoveryPriority>("Low"));
            Assert.Equal(RecoveryPriority.Normal, Enum.Parse<RecoveryPriority>("Normal"));
            Assert.Equal(RecoveryPriority.High, Enum.Parse<RecoveryPriority>("High"));
            Assert.Equal(RecoveryPriority.Critical, Enum.Parse<RecoveryPriority>("Critical"));
        }

        [Fact]
        public void Test_FailureSeverity_Enum_HasCorrectValues()
        {
            Assert.Equal(FailureSeverity.Info, Enum.Parse<FailureSeverity>("Info"));
            Assert.Equal(FailureSeverity.Warning, Enum.Parse<FailureSeverity>("Warning"));
            Assert.Equal(FailureSeverity.Error, Enum.Parse<FailureSeverity>("Error"));
            Assert.Equal(FailureSeverity.Critical, Enum.Parse<FailureSeverity>("Critical"));
            Assert.Equal(FailureSeverity.Fatal, Enum.Parse<FailureSeverity>("Fatal"));
        }

        [Fact]
        public void Test_SecurityValidationState_Enum_HasCorrectValues()
        {
            Assert.Equal(SecurityValidationState.Passed, Enum.Parse<SecurityValidationState>("Passed"));
            Assert.Equal(SecurityValidationState.Failed, Enum.Parse<SecurityValidationState>("Failed"));
            Assert.Equal(SecurityValidationState.Warning, Enum.Parse<SecurityValidationState>("Warning"));
            Assert.Equal(SecurityValidationState.Untrusted, Enum.Parse<SecurityValidationState>("Untrusted"));
            Assert.Equal(SecurityValidationState.Tampered, Enum.Parse<SecurityValidationState>("Tampered"));
        }

        [Fact]
        public void Test_ReportType_Enum_HasCorrectValues()
        {
            Assert.Equal(ReportType.Startup, Enum.Parse<ReportType>("Startup"));
            Assert.Equal(ReportType.Health, Enum.Parse<ReportType>("Health"));
            Assert.Equal(ReportType.Recovery, Enum.Parse<ReportType>("Recovery"));
            Assert.Equal(ReportType.Failure, Enum.Parse<ReportType>("Failure"));
            Assert.Equal(ReportType.Resource, Enum.Parse<ReportType>("Resource"));
            Assert.Equal(ReportType.Security, Enum.Parse<ReportType>("Security"));
        }

        [Fact]
        public void Test_ResourcePressureLevel_Enum_HasCorrectValues()
        {
            Assert.Equal(ResourcePressureLevel.Normal, Enum.Parse<ResourcePressureLevel>("Normal"));
            Assert.Equal(ResourcePressureLevel.Low, Enum.Parse<ResourcePressureLevel>("Low"));
            Assert.Equal(ResourcePressureLevel.Medium, Enum.Parse<ResourcePressureLevel>("Medium"));
            Assert.Equal(ResourcePressureLevel.High, Enum.Parse<ResourcePressureLevel>("High"));
            Assert.Equal(ResourcePressureLevel.Critical, Enum.Parse<ResourcePressureLevel>("Critical"));
        }

        [Fact]
        public void Test_BackoffStrategy_Enum_HasCorrectValues()
        {
            Assert.Equal(BackoffStrategy.Constant, Enum.Parse<BackoffStrategy>("Constant"));
            Assert.Equal(BackoffStrategy.Linear, Enum.Parse<BackoffStrategy>("Linear"));
            Assert.Equal(BackoffStrategy.Exponential, Enum.Parse<BackoffStrategy>("Exponential"));
            Assert.Equal(BackoffStrategy.ExponentialWithJitter, Enum.Parse<BackoffStrategy>("ExponentialWithJitter"));
        }

        [Fact]
        public void Test_RecoveryActionType_Enum_HasCorrectValues()
        {
            Assert.Equal(RecoveryActionType.RestartWorker, Enum.Parse<RecoveryActionType>("RestartWorker"));
            Assert.Equal(RecoveryActionType.ReconnectDatabase, Enum.Parse<RecoveryActionType>("ReconnectDatabase"));
            Assert.Equal(RecoveryActionType.ReconnectTcp, Enum.Parse<RecoveryActionType>("ReconnectTcp"));
            Assert.Equal(RecoveryActionType.ReloadConfiguration, Enum.Parse<RecoveryActionType>("ReloadConfiguration"));
            Assert.Equal(RecoveryActionType.RestartIpc, Enum.Parse<RecoveryActionType>("RestartIpc"));
            Assert.Equal(RecoveryActionType.RestartBackgroundServices, Enum.Parse<RecoveryActionType>("RestartBackgroundServices"));
            Assert.Equal(RecoveryActionType.RestartDownloads, Enum.Parse<RecoveryActionType>("RestartDownloads"));
            Assert.Equal(RecoveryActionType.RestartQueueWorkers, Enum.Parse<RecoveryActionType>("RestartQueueWorkers"));
            Assert.Equal(RecoveryActionType.RestartPluginHost, Enum.Parse<RecoveryActionType>("RestartPluginHost"));
            Assert.Equal(RecoveryActionType.RestartOverlay, Enum.Parse<RecoveryActionType>("RestartOverlay"));
            Assert.Equal(RecoveryActionType.EscalateToAdmin, Enum.Parse<RecoveryActionType>("EscalateToAdmin"));
            Assert.Equal(RecoveryActionType.RebootWorkstation, Enum.Parse<RecoveryActionType>("RebootWorkstation"));
            Assert.Equal(RecoveryActionType.ShutdownWorkstation, Enum.Parse<RecoveryActionType>("ShutdownWorkstation"));
        }

        #endregion

        #region 2. Thread Safety of SubsystemHealthInfo Collections

        [Fact]
        public async Task Test_SubsystemHealthInfo_ThreadSafety_ConcurrentWritesAndReads()
        {
            var healthInfo = new SubsystemHealthInfo
            {
                SubsystemName = "ConcurTest",
                State = SubsystemHealthState.Healthy
            };

            var tasks = new List<Task>();

            // 1. Concurrent history additions
            for (int i = 0; i < 20; i++)
            {
                int index = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        healthInfo.AddHistoryEntry($"Thread {index} Entry {j}");
                    }
                }));
            }

            // 2. Concurrent metadata updates
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 50; j++)
                    {
                        healthInfo.SetMetadata($"Key_{index}_{j}", $"Val_{index}_{j}");
                    }
                }));
            }

            // 3. Concurrent reads/enumerations
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 50; j++)
                    {
                        var history = healthInfo.HealthHistory;
                        var meta = healthInfo.Metadata;
                        var state = healthInfo.State;
                        Assert.NotNull(history);
                        Assert.NotNull(meta);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // History should be capped at 50 entries as designed
            Assert.True(healthInfo.HealthHistory.Count <= 50, $"History list count was {healthInfo.HealthHistory.Count}, expected <= 50.");
            Assert.Equal(500, healthInfo.Metadata.Count);
        }

        #endregion

        #region 3. Model Construction & Default Values Validation

        [Fact]
        public void Test_RecoveryAttempt_Model_Construction_And_Validation()
        {
            var attempt = new RecoveryAttempt
            {
                SubsystemName = "Database",
                ActionTaken = "RESTART_SERVICE",
                AttemptNumber = 2,
                Status = RecoveryStatus.InProgress,
                Message = "Starting retry...",
                ErrorDetails = "NullRef"
            };

            Assert.NotEqual(Guid.Empty, attempt.AttemptId);
            Assert.Equal("Database", attempt.SubsystemName);
            Assert.Equal("RESTART_SERVICE", attempt.ActionTaken);
            Assert.Equal(2, attempt.AttemptNumber);
            Assert.Equal(RecoveryStatus.InProgress, attempt.Status);
            Assert.Equal("Starting retry...", attempt.Message);
            Assert.Equal("NullRef", attempt.ErrorDetails);
            Assert.True((DateTime.UtcNow - attempt.Timestamp).TotalSeconds < 5);
        }

        [Fact]
        public void Test_RecoveryResult_Model_Construction_And_Validation()
        {
            var id = Guid.NewGuid();
            var duration = TimeSpan.FromSeconds(3);
            var result = new RecoveryResult
            {
                AttemptId = id,
                SubsystemName = "Network",
                IsSuccessful = true,
                FinalStatus = RecoveryStatus.Success,
                Duration = duration,
                OutputMessage = "Completed successfully.",
                ErrorDetails = null
            };

            Assert.Equal(id, result.AttemptId);
            Assert.Equal("Network", result.SubsystemName);
            Assert.True(result.IsSuccessful);
            Assert.Equal(RecoveryStatus.Success, result.FinalStatus);
            Assert.Equal(duration, result.Duration);
            Assert.Equal("Completed successfully.", result.OutputMessage);
            Assert.Null(result.ErrorDetails);
        }

        [Fact]
        public void Test_ResourceMetrics_Model_Construction_And_Validation()
        {
            var metrics = new ResourceMetrics
            {
                CpuUsagePercentage = 75.5,
                ProcessRamBytes = 512 * 1024 * 1024L,
                TotalSystemRamBytes = 16384 * 1024 * 1024L,
                AvailableSystemRamBytes = 8192 * 1024 * 1024L,
                FreeDiskSpaceBytes = 100 * 1024 * 1024 * 1024L,
                HandleCount = 800,
                ThreadCount = 45,
                GdiObjectsCount = 150,
                GpuUsagePercentage = 15.0,
                DiskIoBytesPerSecond = 500000.0,
                NetworkIoBytesPerSecond = 120000.0,
                HardwareTemperatureCelsius = 62.5,
                PressureLevel = ResourcePressureLevel.High
            };

            Assert.Equal(75.5, metrics.CpuUsagePercentage);
            Assert.Equal(512 * 1024 * 1024L, metrics.ProcessRamBytes);
            Assert.Equal(16384 * 1024 * 1024L, metrics.TotalSystemRamBytes);
            Assert.Equal(8192 * 1024 * 1024L, metrics.AvailableSystemRamBytes);
            Assert.Equal(100 * 1024 * 1024 * 1024L, metrics.FreeDiskSpaceBytes);
            Assert.Equal(800, metrics.HandleCount);
            Assert.Equal(45, metrics.ThreadCount);
            Assert.Equal(150, metrics.GdiObjectsCount);
            Assert.Equal(15.0, metrics.GpuUsagePercentage);
            Assert.Equal(500000.0, metrics.DiskIoBytesPerSecond);
            Assert.Equal(120000.0, metrics.NetworkIoBytesPerSecond);
            Assert.Equal(62.5, metrics.HardwareTemperatureCelsius);
            Assert.Equal(ResourcePressureLevel.High, metrics.PressureLevel);
        }

        [Fact]
        public void Test_SecurityValidationResult_Model_Construction_And_Validation()
        {
            var result = new SecurityValidationResult
            {
                TargetName = "appsettings.json",
                ValidationState = SecurityValidationState.Tampered,
                ExpectedSignature = "expected_hash_sig",
                ComputedSignature = "computed_hash_sig",
                Message = "File signature mismatch."
            };

            Assert.NotEqual(Guid.Empty, result.CheckId);
            Assert.Equal("appsettings.json", result.TargetName);
            Assert.Equal(SecurityValidationState.Tampered, result.ValidationState);
            Assert.Equal("expected_hash_sig", result.ExpectedSignature);
            Assert.Equal("computed_hash_sig", result.ComputedSignature);
            Assert.Equal("File signature mismatch.", result.Message);
            Assert.True(result.IsTamperDetected);
        }

        [Fact]
        public void Test_FailureRecord_Model_Construction_And_Validation()
        {
            var record = new FailureRecord
            {
                SubsystemName = "IPC",
                Severity = FailureSeverity.Critical,
                ErrorMessage = "Named pipe listener crashed.",
                ExceptionTrace = "Exception stack trace details"
            };

            Assert.NotEqual(Guid.Empty, record.RecordId);
            Assert.NotEqual(Guid.Empty, record.CorrelationId);
            Assert.Equal("IPC", record.SubsystemName);
            Assert.Equal(FailureSeverity.Critical, record.Severity);
            Assert.Equal("Named pipe listener crashed.", record.ErrorMessage);
            Assert.Equal("Exception stack trace details", record.ExceptionTrace);
        }

        #endregion

        #region 4. Serialization / Deserialization Tests

        [Fact]
        public void Test_SubsystemHealthInfo_SerializationRoundTrip()
        {
            var original = new SubsystemHealthInfo
            {
                SubsystemName = "TelemSync",
                State = SubsystemHealthState.Warning,
                FailureCount = 2,
                RecoveryCount = 1,
                HealthScore = 85.0,
                LastRecovery = DateTime.UtcNow.AddMinutes(-5),
                LastMessage = "Stale queue detected.",
                LastException = "TimeoutException stack trace..."
            };
            original.Dependencies.Add("Database");
            original.AddHistoryEntry("2024-03-31 12:00:00 - Online");
            original.SetMetadata("QueueBacklog", "150");

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<SubsystemHealthInfo>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(original.SubsystemName, deserialized.SubsystemName);
            Assert.Equal(original.State, deserialized.State);
            Assert.Equal(original.PreviousState, deserialized.PreviousState);
            Assert.Equal(original.FailureCount, deserialized.FailureCount);
            Assert.Equal(original.RecoveryCount, deserialized.RecoveryCount);
            Assert.Equal(original.HealthScore, deserialized.HealthScore);
            Assert.Equal(original.LastMessage, deserialized.LastMessage);
            Assert.Equal(original.LastException, deserialized.LastException);
            Assert.Contains("Database", deserialized.Dependencies);
            Assert.Contains("QueueBacklog", deserialized.Metadata.Keys);
            Assert.Equal("150", deserialized.GetMetadata("QueueBacklog"));
        }

        [Fact]
        public void Test_RecoveryPolicy_SerializationRoundTrip()
        {
            var original = new RecoveryPolicy
            {
                SubsystemName = "Database",
                IsEnabled = true,
                Priority = RecoveryPriority.Critical,
                DefaultAction = RecoveryActionType.ReconnectDatabase,
                Retry = new RetryPolicy
                {
                    MaxRetries = 4,
                    InitialDelay = TimeSpan.FromSeconds(3),
                    MaxDelay = TimeSpan.FromSeconds(20),
                    BackoffStrategy = BackoffStrategy.Exponential
                },
                Cooldown = new CooldownPolicy
                {
                    CooldownDuration = TimeSpan.FromMinutes(3),
                    EvaluationWindow = TimeSpan.FromMinutes(1),
                    FailureThreshold = 2,
                    SuspendHealingDuringCooldown = true
                },
                Escalation = new EscalationPolicy
                {
                    EscalationSequence = new List<RecoveryActionType> { RecoveryActionType.EscalateToAdmin, RecoveryActionType.RebootWorkstation },
                    AttemptsBeforeEscalation = 4,
                    NotifyAdminOnEscalation = true,
                    RebootAuthorized = true,
                    EscalationTimeout = TimeSpan.FromSeconds(45)
                },
                Dependency = new DependencyPolicy
                {
                    PreRecoveryDependencies = new List<string> { "StorageService" },
                    PropagateFailures = true,
                    CascadeRecovery = false,
                    FailClosedOnDependencyFailure = true
                },
                LimitConfig = new MaximumRetryConfiguration
                {
                    AbsoluteMaxRetries = 6,
                    MaxRetriesInWindow = 4,
                    RollingWindowDuration = TimeSpan.FromMinutes(10),
                    DisableHealingOnThresholdExceeded = true
                }
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<RecoveryPolicy>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(original.SubsystemName, deserialized.SubsystemName);
            Assert.Equal(original.IsEnabled, deserialized.IsEnabled);
            Assert.Equal(original.Priority, deserialized.Priority);
            Assert.Equal(original.DefaultAction, deserialized.DefaultAction);
            Assert.Equal(original.Retry.MaxRetries, deserialized.Retry.MaxRetries);
            Assert.Equal(original.Retry.InitialDelay, deserialized.Retry.InitialDelay);
            Assert.Equal(original.Retry.BackoffStrategy, deserialized.Retry.BackoffStrategy);
            Assert.Equal(original.Cooldown.CooldownDuration, deserialized.Cooldown.CooldownDuration);
            Assert.Equal(original.Escalation.EscalationSequence[0], deserialized.Escalation.EscalationSequence[0]);
            Assert.Equal(original.Dependency.PreRecoveryDependencies[0], deserialized.Dependency.PreRecoveryDependencies[0]);
            Assert.Equal(original.LimitConfig.AbsoluteMaxRetries, deserialized.LimitConfig.AbsoluteMaxRetries);
        }

        #endregion

        #region 5. Logical Model Equality Tests

        [Fact]
        public void Test_SubsystemHealthInfo_EqualityAndDefaults()
        {
            var info1 = new SubsystemHealthInfo();
            var info2 = new SubsystemHealthInfo();

            Assert.Equal(SubsystemHealthState.Healthy, info1.State);
            Assert.Equal(SubsystemHealthState.Healthy, info2.State);
            Assert.Equal(string.Empty, info1.SubsystemName);
            Assert.Equal(100.0, info1.HealthScore);
            Assert.NotNull(info1.Dependencies);
            Assert.NotNull(info1.HealthHistory);
            Assert.NotNull(info1.Metadata);
        }

        #endregion
    }
}
