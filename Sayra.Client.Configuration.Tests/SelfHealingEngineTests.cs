using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Events;
using Sayra.Client.Shared.Models.Recovery.Policies;
using SayraClient.Services.Recovery;
using SayraClient.Services.Recovery.Strategies;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    [Collection("Stage7Tests")]
    public class SelfHealingEngineTests
    {
        private readonly Mock<IHealthMonitor> _healthMonitorMock = new();
        private readonly Mock<IEventDispatcher> _eventDispatcherMock = new();
        private readonly RecoveryQueue _queue;
        private readonly LoopDetector _loopDetector;
        private readonly RecoveryDependencyResolver _dependencyResolver;
        private readonly RecoveryMetricsCollector _metricsCollector;
        private readonly BackoffDelayCalculator _backoffCalculator;
        private readonly List<IRecoveryActionStrategy> _strategies = new();

        public SelfHealingEngineTests()
        {
            _queue = new RecoveryQueue(NullLogger<RecoveryQueue>.Instance);
            _loopDetector = new LoopDetector();
            _dependencyResolver = new RecoveryDependencyResolver();
            _metricsCollector = new RecoveryMetricsCollector();
            _backoffCalculator = new BackoffDelayCalculator();

            _healthMonitorMock.Setup(h => h.GetSubsystemHealth(It.IsAny<string>())).Returns(SubsystemHealthState.Healthy);
        }

        [Fact]
        public async Task Test_SuccessfulRecoveryExecution()
        {
            // Arrange
            var strategyMock = new Mock<IRecoveryActionStrategy>();
            strategyMock.Setup(s => s.ActionType).Returns(RecoveryActionType.ReconnectDatabase);
            strategyMock.Setup(s => s.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _strategies.Add(strategyMock.Object);

            using var service = new SelfHealingService(
                NullLogger<SelfHealingService>.Instance,
                _healthMonitorMock.Object,
                _eventDispatcherMock.Object,
                _queue,
                _loopDetector,
                _dependencyResolver,
                _metricsCollector,
                _backoffCalculator,
                _strategies);

            // Act
            await service.RecoverSubsystemAsync("Database");

            // Wait a brief moment for the background queue processor task to process the queued recovery
            await Task.Delay(100);

            // Assert
            strategyMock.Verify(s => s.ExecuteAsync("Database", It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(1, _metricsCollector.SuccessCount);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<RecoveryCompletedEvent>()), Times.Once);
        }

        [Fact]
        public async Task Test_FailedRecovery_RetriesAndEscalates()
        {
            // Arrange
            var strategyMock = new Mock<IRecoveryActionStrategy>();
            strategyMock.Setup(s => s.ActionType).Returns(RecoveryActionType.ReconnectDatabase);
            strategyMock.Setup(s => s.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _strategies.Add(strategyMock.Object);

            using var service = new SelfHealingService(
                NullLogger<SelfHealingService>.Instance,
                _healthMonitorMock.Object,
                _eventDispatcherMock.Object,
                _queue,
                _loopDetector,
                _dependencyResolver,
                _metricsCollector,
                _backoffCalculator,
                _strategies);

            // Configure linear policy for zero-delay test
            var policy = new RecoveryPolicy
            {
                SubsystemName = "Database",
                IsEnabled = true,
                Priority = RecoveryPriority.Normal,
                DefaultAction = RecoveryActionType.ReconnectDatabase,
                Retry = new RetryPolicy { MaxRetries = 2, InitialDelay = TimeSpan.Zero, BackoffStrategy = BackoffStrategy.Constant },
                Cooldown = new CooldownPolicy { CooldownDuration = TimeSpan.FromSeconds(2), EvaluationWindow = TimeSpan.FromSeconds(30), FailureThreshold = 1 }
            };
            service.RegisterPolicy(policy);

            // Act
            await service.RecoverSubsystemAsync("Database");

            // Assert
            strategyMock.Verify(s => s.ExecuteAsync("Database", It.IsAny<CancellationToken>()), Times.Exactly(3)); // 1 initial + 2 retries
            Assert.Equal(1, _metricsCollector.FailureCount);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<RecoveryFailedEvent>()), Times.Once);
        }

        [Fact]
        public void Test_BackoffDelayCalculator_CalculatesCorrectDelays()
        {
            // Arrange
            var linearPolicy = new RetryPolicy { InitialDelay = TimeSpan.FromSeconds(2), MaxDelay = TimeSpan.FromSeconds(10), BackoffStrategy = BackoffStrategy.Linear };
            var exponentialPolicy = new RetryPolicy { InitialDelay = TimeSpan.FromSeconds(2), MaxDelay = TimeSpan.FromSeconds(10), BackoffStrategy = BackoffStrategy.Exponential };

            // Act & Assert
            Assert.Equal(TimeSpan.FromSeconds(4), _backoffCalculator.CalculateDelay(2, linearPolicy));
            Assert.Equal(TimeSpan.FromSeconds(6), _backoffCalculator.CalculateDelay(3, linearPolicy));

            Assert.Equal(TimeSpan.FromSeconds(4), _backoffCalculator.CalculateDelay(2, exponentialPolicy));
            Assert.Equal(TimeSpan.FromSeconds(8), _backoffCalculator.CalculateDelay(3, exponentialPolicy));
            Assert.Equal(TimeSpan.FromSeconds(10), _backoffCalculator.CalculateDelay(4, exponentialPolicy)); // Cap at max delay
        }

        [Fact]
        public async Task Test_CooldownHandling_BlocksRecovery()
        {
            // Arrange
            var strategyMock = new Mock<IRecoveryActionStrategy>();
            strategyMock.Setup(s => s.ActionType).Returns(RecoveryActionType.ReconnectDatabase);
            strategyMock.Setup(s => s.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _strategies.Add(strategyMock.Object);

            using var service = new SelfHealingService(
                NullLogger<SelfHealingService>.Instance,
                _healthMonitorMock.Object,
                _eventDispatcherMock.Object,
                _queue,
                _loopDetector,
                _dependencyResolver,
                _metricsCollector,
                _backoffCalculator,
                _strategies);

            var policy = new RecoveryPolicy
            {
                SubsystemName = "Database",
                IsEnabled = true,
                DefaultAction = RecoveryActionType.ReconnectDatabase,
                Cooldown = new CooldownPolicy { CooldownDuration = TimeSpan.FromSeconds(10), EvaluationWindow = TimeSpan.FromSeconds(30), FailureThreshold = 2 }
            };
            service.RegisterPolicy(policy);

            // Record 2 failures within window to trigger cooldown
            _loopDetector.RecordFailure("Database");
            _loopDetector.RecordFailure("Database");

            // Act
            await service.RecoverSubsystemAsync("Database");

            // Assert
            strategyMock.Verify(s => s.ExecuteAsync("Database", It.IsAny<CancellationToken>()), Times.Never);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<RecoveryLoopDetectedEvent>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Test_LoopDetection_EscalatesFailure()
        {
            // Arrange
            var strategyMock = new Mock<IRecoveryActionStrategy>();
            strategyMock.Setup(s => s.ActionType).Returns(RecoveryActionType.ReconnectDatabase);
            strategyMock.Setup(s => s.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _strategies.Add(strategyMock.Object);

            using var service = new SelfHealingService(
                NullLogger<SelfHealingService>.Instance,
                _healthMonitorMock.Object,
                _eventDispatcherMock.Object,
                _queue,
                _loopDetector,
                _dependencyResolver,
                _metricsCollector,
                _backoffCalculator,
                _strategies);

            var policy = new RecoveryPolicy
            {
                SubsystemName = "Database",
                IsEnabled = true,
                DefaultAction = RecoveryActionType.ReconnectDatabase,
                Cooldown = new CooldownPolicy { CooldownDuration = TimeSpan.FromSeconds(10), EvaluationWindow = TimeSpan.FromSeconds(30), FailureThreshold = 1 }
            };
            service.RegisterPolicy(policy);

            // Record a failure
            _loopDetector.RecordFailure("Database");

            // Act
            await service.RecoverSubsystemAsync("Database");

            // Assert
            Assert.True(_loopDetector.IsEscalated("Database"));
            Assert.Equal(1, _metricsCollector.EscalationCount);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<RecoveryEscalatedEvent>()), Times.Once);
        }

        [Fact]
        public async Task Test_DependencyBlocking_SkipsRecovery()
        {
            // Arrange
            var strategyMock = new Mock<IRecoveryActionStrategy>();
            strategyMock.Setup(s => s.ActionType).Returns(RecoveryActionType.ReloadConfiguration);
            strategyMock.Setup(s => s.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _strategies.Add(strategyMock.Object);

            _healthMonitorMock.Setup(h => h.GetSubsystemHealth("Database")).Returns(SubsystemHealthState.Critical);

            using var service = new SelfHealingService(
                NullLogger<SelfHealingService>.Instance,
                _healthMonitorMock.Object,
                _eventDispatcherMock.Object,
                _queue,
                _loopDetector,
                _dependencyResolver,
                _metricsCollector,
                _backoffCalculator,
                _strategies);

            // Act
            await service.RecoverSubsystemAsync("PolicyEngine");

            // Assert
            strategyMock.Verify(s => s.ExecuteAsync("PolicyEngine", It.IsAny<CancellationToken>()), Times.Never);
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<RecoveryDependencyBlockedEvent>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Test_RecoveryQueueOrdering_FIFOByPriority()
        {
            // Act - Do NOT await the task returned by EnqueueAsync to prevent infinite waiting
            var t1 = _queue.EnqueueAsync("SubsystemA", RecoveryPriority.Low);
            var t2 = _queue.EnqueueAsync("SubsystemB", RecoveryPriority.High);
            var t3 = _queue.EnqueueAsync("SubsystemC", RecoveryPriority.Critical);

            var item1 = await _queue.DequeueAsync(CancellationToken.None);
            var item2 = await _queue.DequeueAsync(CancellationToken.None);
            var item3 = await _queue.DequeueAsync(CancellationToken.None);

            // Assert
            Assert.Equal("SubsystemC", item1!.SubsystemName);
            Assert.Equal("SubsystemB", item2!.SubsystemName);
            Assert.Equal("SubsystemA", item3!.SubsystemName);
        }

        [Fact]
        public async Task Test_ConcurrentRecovery_ExecutesInParallel()
        {
            // Arrange
            var strategyMock = new Mock<IRecoveryActionStrategy>();
            strategyMock.Setup(s => s.ActionType).Returns(RecoveryActionType.ReconnectDatabase);
            strategyMock.Setup(s => s.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .Returns(async () =>
                        {
                            await Task.Delay(100);
                            return true;
                        });
            _strategies.Add(strategyMock.Object);

            using var service = new SelfHealingService(
                NullLogger<SelfHealingService>.Instance,
                _healthMonitorMock.Object,
                _eventDispatcherMock.Object,
                _queue,
                _loopDetector,
                _dependencyResolver,
                _metricsCollector,
                _backoffCalculator,
                _strategies);

            // Act
            var task1 = service.RecoverSubsystemAsync("Database");
            var task2 = service.RecoverSubsystemAsync("Database");

            await Task.WhenAll(task1, task2);

            // Assert
            Assert.Equal(1, _metricsCollector.SuccessCount); // Deduplicated enqueued items!
        }

        [Fact]
        public async Task Test_CancellationHandling_CancelsExecutionAndPublishesEvent()
        {
            // Arrange
            var strategyMock = new Mock<IRecoveryActionStrategy>();
            strategyMock.Setup(s => s.ActionType).Returns(RecoveryActionType.ReconnectDatabase);
            strategyMock.Setup(s => s.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .Returns(async (string sub, CancellationToken ct) =>
                        {
                            await Task.Delay(2000, ct); // Large delay to allow cancellation to trigger
                            return true;
                        });
            _strategies.Add(strategyMock.Object);

            using var service = new SelfHealingService(
                NullLogger<SelfHealingService>.Instance,
                _healthMonitorMock.Object,
                _eventDispatcherMock.Object,
                _queue,
                _loopDetector,
                _dependencyResolver,
                _metricsCollector,
                _backoffCalculator,
                _strategies);

            using var cts = new CancellationTokenSource();

            // Act
            var task = service.RecoverSubsystemAsync("Database", cts.Token);
            cts.Cancel(); // Immediately cancel

            await task; // Should complete gracefully because it catches internally

            // Assert
            _eventDispatcherMock.Verify(d => d.Dispatch(It.IsAny<RecoveryCancelledEvent>()), Times.AtLeastOnce);
        }
    }
}
