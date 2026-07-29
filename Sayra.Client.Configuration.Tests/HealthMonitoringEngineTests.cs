using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;
using SayraClient.Services.Recovery;

namespace Sayra.Client.Configuration.Tests
{
    [Collection("Stage7Tests")]
    public class HealthMonitoringEngineTests
    {
        private readonly HealthMonitorOptions _options;
        private readonly IOptions<HealthMonitorOptions> _optionsWrapper;

        public HealthMonitoringEngineTests()
        {
            _options = new HealthMonitorOptions
            {
                DefaultHeartbeatTimeout = TimeSpan.FromSeconds(2),
                BaseFailureDeduction = 8.0,
                BaseTransitionDeduction = 4.0,
                DependencyFailureDeduction = 12.0,
                MaxHistoricalSnapshots = 5
            };
            _optionsWrapper = Options.Create(_options);
        }

        #region (a) Dynamic Subsystem Registration & Dependency Graph Representation

        [Fact]
        public void Test_DynamicRegistration_PopulatesSubsystemDetailsCorrectly()
        {
            // Arrange
            using var monitor = new HealthMonitor(NullLogger<HealthMonitor>.Instance, _optionsWrapper);
            var dependencies = new List<string> { "Database", "Telemetry" };

            // Act
            monitor.RegisterSubsystem("BillingModule", dependencies);
            var details = monitor.GetSubsystemDetails("BillingModule");

            // Assert
            Assert.NotNull(details);
            Assert.Equal("BillingModule", details.SubsystemId);
            Assert.Equal("BillingModule", details.SubsystemName);
            Assert.Equal("BillingModule", details.DisplayName);
            Assert.Equal(SubsystemHealthState.Healthy, details.State);
            Assert.Equal(100.0, details.HealthScore);
            Assert.Equal(2, details.Dependencies.Count);
            Assert.Contains("Database", details.Dependencies);
            Assert.Contains("Telemetry", details.Dependencies);
        }

        [Fact]
        public void Test_UnregisterSubsystem_RemovesItSuccessfully()
        {
            // Arrange
            using var monitor = new HealthMonitor(NullLogger<HealthMonitor>.Instance, _optionsWrapper);
            monitor.RegisterSubsystem("TempModule", new List<string>());

            // Act
            monitor.UnregisterSubsystem("TempModule");
            var details = monitor.GetSubsystemDetails("TempModule");

            // Assert
            Assert.Null(details);
        }

        #endregion

        #region (b) Configurable Heartbeat Timeouts & Automatic Stale Downgrading

        [Fact]
        public async Task Test_HeartbeatTimeout_DowngradesSubsystemToWarning()
        {
            // Arrange
            var customOptions = new HealthMonitorOptions
            {
                DefaultHeartbeatTimeout = TimeSpan.FromMilliseconds(200)
            };
            using var monitor = new HealthMonitor(NullLogger<HealthMonitor>.Instance, Options.Create(customOptions));
            monitor.RegisterSubsystem("FastTimeoutModule", new List<string>());

            // Act & Assert (Immediately Healthy)
            Assert.Equal(SubsystemHealthState.Healthy, monitor.GetSubsystemHealth("FastTimeoutModule"));

            // Wait for heartbeat timeout to expire
            await Task.Delay(350);

            // Accessing health state evaluates timeouts
            var state = monitor.GetSubsystemHealth("FastTimeoutModule");

            // Assert - State must downgrade to Warning
            Assert.Equal(SubsystemHealthState.Warning, state);
        }

        [Fact]
        public void Test_ReportHeartbeat_RestoresStateFromDegradedToHealthy()
        {
            // Arrange
            using var monitor = new HealthMonitor(NullLogger<HealthMonitor>.Instance, _optionsWrapper);
            monitor.RegisterSubsystem("RecoverableModule", new List<string>());

            // Transition to Offline manually
            monitor.ReportSubsystemState("RecoverableModule", SubsystemHealthState.Offline, "Forced stop");
            Assert.Equal(SubsystemHealthState.Offline, monitor.GetSubsystemHealth("RecoverableModule"));

            // Act - Send heartbeat
            monitor.ReportHeartbeat("RecoverableModule");

            // Assert
            Assert.Equal(SubsystemHealthState.Healthy, monitor.GetSubsystemHealth("RecoverableModule"));
        }

        #endregion

        #region (c) Concurrent State Transitions & History Limit Enforcement

        [Fact]
        public void Test_TransitionHistory_EnforcesCappedCapacityOf50()
        {
            // Arrange
            using var monitor = new HealthMonitor(NullLogger<HealthMonitor>.Instance, _optionsWrapper);
            monitor.RegisterSubsystem("HistoryCapModule", new List<string>());

            // Act - Perform 60 transitions
            for (int i = 0; i < 60; i++)
            {
                var targetState = (i % 2 == 0) ? SubsystemHealthState.Warning : SubsystemHealthState.Healthy;
                monitor.ReportSubsystemState("HistoryCapModule", targetState, $"Transition #{i}");
            }

            var history = monitor.GetTransitionHistory("HistoryCapModule");

            // Assert
            Assert.NotNull(history);
            Assert.True(history.Count <= 50, $"History size is {history.Count}, which exceeds cap of 50!");
        }

        [Fact]
        public async Task Test_ConcurrentAccess_IsFullyThreadSafe()
        {
            // Arrange
            using var monitor = new HealthMonitor(NullLogger<HealthMonitor>.Instance, _optionsWrapper);
            monitor.RegisterSubsystem("ConcurrentModule", new List<string>());

            int taskCount = 20;
            int iterationsPerTask = 50;
            var tasks = new List<Task>();

            // Act - Concurrently update states and report heartbeats from multiple threads
            for (int i = 0; i < taskCount; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < iterationsPerTask; j++)
                    {
                        if (j % 2 == 0)
                        {
                            monitor.ReportHeartbeat("ConcurrentModule");
                        }
                        else
                        {
                            var state = (j % 3 == 0) ? SubsystemHealthState.Warning : SubsystemHealthState.Healthy;
                            monitor.ReportSubsystemState("ConcurrentModule", state, $"State transition from task {taskId}");
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - The engine should remain completely stable, no exceptions thrown
            var details = monitor.GetSubsystemDetails("ConcurrentModule");
            Assert.NotNull(details);
            Assert.True(details.HealthHistory.Count <= 50);
        }

        #endregion

        #region (d) Health Score Math under Varying Degradation Levels

        [Fact]
        public void Test_HealthScoreCalculation_AppliesCorrectStateDeductions()
        {
            // Arrange
            using var monitor = new HealthMonitor(NullLogger<HealthMonitor>.Instance, _optionsWrapper);
            monitor.RegisterSubsystem("ScoreModule", new List<string>());

            // Act & Assert: Healthy state -> 100 points
            Assert.Equal(100.0, monitor.GetSubsystemDetails("ScoreModule")!.HealthScore);

            // Act & Assert: Warning state -> 76 points (100 - 20 state deduction - 4 history deduction)
            monitor.ReportSubsystemState("ScoreModule", SubsystemHealthState.Warning, "Minor warning");
            Assert.Equal(76.0, monitor.GetSubsystemDetails("ScoreModule")!.HealthScore);

            // Act & Assert: Critical state -> 24 points
            monitor.ReportSubsystemState("ScoreModule", SubsystemHealthState.Critical, "Major critical");
            // Let's verify the exact score based on math:
            // Base state critical: -60 -> 40 points
            // Failure count = 1 -> deduction is BaseFailureDeduction (8.0) -> final is 32 points.
            // Transitions in history: 2 transitions containing "State:" -> deduction is 2 * 4.0 = 8.0 points.
            // Expected score = 100 - 60 - 8 - 8 = 24.0.
            Assert.Equal(24.0, monitor.GetSubsystemDetails("ScoreModule")!.HealthScore);
        }

        [Fact]
        public void Test_DependencyFailure_DeductsFromScore()
        {
            // Arrange
            using var monitor = new HealthMonitor(NullLogger<HealthMonitor>.Instance, _optionsWrapper);
            monitor.RegisterSubsystem("ParentModule", new List<string> { "ChildModule" });
            monitor.RegisterSubsystem("ChildModule", new List<string>());

            // Confirm parent is healthy
            Assert.Equal(100.0, monitor.GetSubsystemDetails("ParentModule")!.HealthScore);

            // Act - Transition Child to Critical
            monitor.ReportSubsystemState("ChildModule", SubsystemHealthState.Critical, "Child failed!");

            // Trigger Evaluation on parent
            _ = monitor.GetSubsystemHealth("ParentModule");

            // Assert
            var parentDetails = monitor.GetSubsystemDetails("ParentModule");
            // Parent has been automatically transitioned to Warning because dependency is Critical/Offline,
            // or parent score has a direct deduction for failed dependency.
            // Let's inspect that score has deducted dependency failure points.
            Assert.True(parentDetails!.HealthScore < 100.0);
        }

        #endregion

        #region (e) Immutable Snapshots (Current, Historical, Subsystem, Global)

        [Fact]
        public void Test_GetCurrentSnapshot_IsFullyImmutableAndCloned()
        {
            // Arrange
            using var monitor = new HealthMonitor(NullLogger<HealthMonitor>.Instance, _optionsWrapper);
            monitor.RegisterSubsystem("SnapshotModule", new List<string>());

            // Act - Capture snapshot
            var snapshot1 = monitor.GetCurrentSnapshot();
            var modInfo = snapshot1.DetailedSubsystems.First(s => s.SubsystemName == "SnapshotModule");

            // Transition state in engine
            monitor.ReportSubsystemState("SnapshotModule", SubsystemHealthState.Critical, "Crash");

            var snapshot2 = monitor.GetCurrentSnapshot();
            var modInfoAfter = snapshot2.DetailedSubsystems.First(s => s.SubsystemName == "SnapshotModule");

            // Assert - Modification of engine state must not alter previously captured snapshot details
            Assert.Equal(SubsystemHealthState.Healthy, modInfo.State);
            Assert.Equal(SubsystemHealthState.Critical, modInfoAfter.State);
        }

        [Fact]
        public void Test_HistoricalSnapshots_RingBuffer_RespectsCappedLimit()
        {
            // Arrange
            using var monitor = new HealthMonitor(NullLogger<HealthMonitor>.Instance, _optionsWrapper); // Max historical snapshots is 5

            // Act - Generate 10 snapshots
            for (int i = 0; i < 10; i++)
            {
                monitor.GetCurrentSnapshot();
            }

            var historical = monitor.GetHistoricalSnapshots();

            // Assert - Must cap at 5
            Assert.True(historical.Count <= 5);
        }

        #endregion

        #region (f) Strongly-Typed Event Publishing

        [Fact]
        public void Test_EventsAreFiredWithCorrectStronglyTypedArguments()
        {
            // Arrange
            using var monitor = new HealthMonitor(NullLogger<HealthMonitor>.Instance, _optionsWrapper);

            SubsystemRegisteredEventArgs? regArgs = null;
            HeartbeatUpdatedEventArgs? hbArgs = null;
            StateChangedEventArgs? stateArgs = null;
            FailureRecordedEventArgs? failArgs = null;
            HealthScoreChangedEventArgs? scoreArgs = null;

            monitor.SubsystemRegistered += (sender, e) => regArgs = e;
            monitor.HeartbeatUpdated += (sender, e) => hbArgs = e;
            monitor.StateChanged += (sender, e) => stateArgs = e;
            monitor.FailureRecorded += (sender, e) => failArgs = e;
            monitor.HealthScoreChanged += (sender, e) => scoreArgs = e;

            // Act 1: Register
            monitor.RegisterSubsystem("EventTestModule", new List<string> { "Database" });
            Assert.NotNull(regArgs);
            Assert.Equal("EventTestModule", regArgs.SubsystemName);
            Assert.Contains("Database", regArgs.Dependencies);

            // Act 2: Heartbeat
            monitor.ReportHeartbeat("EventTestModule");
            Assert.NotNull(hbArgs);
            Assert.Equal("EventTestModule", hbArgs.SubsystemName);
            Assert.True((DateTime.UtcNow - hbArgs.Timestamp).TotalSeconds < 1);

            // Act 3: State Transition & Failure
            monitor.ReportSubsystemState("EventTestModule", SubsystemHealthState.Critical, "Event failure", "EventStackException");

            Assert.NotNull(stateArgs);
            Assert.Equal("EventTestModule", stateArgs.SubsystemName);
            Assert.Equal(SubsystemHealthState.Healthy, stateArgs.OldState);
            Assert.Equal(SubsystemHealthState.Critical, stateArgs.NewState);
            Assert.Equal("Event failure", stateArgs.Message);

            Assert.NotNull(failArgs);
            Assert.Equal("EventTestModule", failArgs.SubsystemName);
            Assert.Equal("Event failure", failArgs.ErrorMessage);
            Assert.Equal("EventStackException", failArgs.ExceptionDetails);

            Assert.NotNull(scoreArgs);
            Assert.Equal("EventTestModule", scoreArgs.SubsystemName);
            Assert.Equal(100.0, scoreArgs.OldScore);
            Assert.True(scoreArgs.NewScore < 100.0);
        }

        #endregion
    }
}
