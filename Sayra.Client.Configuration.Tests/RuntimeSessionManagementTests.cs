using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Application.Services;
using Sayra.Client.Shared.Runtime.Domain.Entities;
using Sayra.Client.Shared.Runtime.Domain.Events;
using Sayra.Client.Shared.Runtime.Domain.States;
using Sayra.Client.Shared.Runtime.Infrastructure.Persistence;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    public class RuntimeSessionManagementTests
    {
        private readonly Mock<ILogger<RuntimeStateManager>> _stateManagerLoggerMock = new();
        private readonly Mock<ILogger<RuntimeSessionManager>> _sessionManagerLoggerMock = new();
        private readonly Mock<ILogger<SessionTimerService>> _timerServiceLoggerMock = new();
        private readonly Mock<ILogger<SessionExpirationHandler>> _expirationHandlerLoggerMock = new();
        private readonly Mock<IProcessSupervisor> _processSupervisorMock = new();

        private readonly IRuntimeEventPublisher _eventPublisher = new RuntimeEventPublisher(null);
        private readonly ISessionRepository _sessionRepository = new InMemorySessionRepository();

        #region State Machine Tests

        [Fact]
        public void StateMachine_TransitionsIncludingWarningAndExpired_ShouldSucceed()
        {
            var stateManager = new RuntimeStateManager(_stateManagerLoggerMock.Object, _eventPublisher);

            // Transition: Created -> Preparing -> Starting -> Running -> Warning -> Expired -> Stopping -> Completed
            stateManager.TransitionTo(RuntimeState.Preparing);
            Assert.Equal(RuntimeState.Preparing, stateManager.CurrentState);

            stateManager.TransitionTo(RuntimeState.Starting);
            Assert.Equal(RuntimeState.Starting, stateManager.CurrentState);

            stateManager.TransitionTo(RuntimeState.Running);
            Assert.Equal(RuntimeState.Running, stateManager.CurrentState);

            stateManager.TransitionTo(RuntimeState.Warning);
            Assert.Equal(RuntimeState.Warning, stateManager.CurrentState);

            stateManager.TransitionTo(RuntimeState.Expired);
            Assert.Equal(RuntimeState.Expired, stateManager.CurrentState);

            stateManager.TransitionTo(RuntimeState.Stopping);
            Assert.Equal(RuntimeState.Stopping, stateManager.CurrentState);

            stateManager.TransitionTo(RuntimeState.Completed);
            Assert.Equal(RuntimeState.Completed, stateManager.CurrentState);
        }

        #endregion

        #region Timer Service Tests

        [Fact]
        public void TimerService_StartAndRetrieveTimes_ShouldCalculateCorrectly()
        {
            using var timerService = new SessionTimerService(_timerServiceLoggerMock.Object);
            var sessionId = Guid.NewGuid();
            var totalTime = TimeSpan.FromMinutes(60);

            timerService.StartTracking(sessionId, totalTime, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(5));

            var remaining = timerService.GetRemainingTime(sessionId);
            var elapsed = timerService.GetElapsedTime(sessionId);

            Assert.True(remaining > TimeSpan.FromMinutes(59) && remaining <= totalTime);
            Assert.True(elapsed >= TimeSpan.Zero && elapsed < TimeSpan.FromSeconds(2));

            timerService.StopTracking(sessionId);
            Assert.Equal(TimeSpan.Zero, timerService.GetRemainingTime(sessionId));
        }

        [Fact]
        public void TimerService_TriggerWarningsAndExpiration_ViaManualTick()
        {
            using var timerService = new SessionTimerService(_timerServiceLoggerMock.Object);
            var sessionId = Guid.NewGuid();

            // Total time: 10 seconds. Threshold 1: 5 seconds. Threshold 2: 2 seconds.
            timerService.StartTracking(sessionId, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));

            List<SessionWarningEvent> warnings = new();
            Guid expiredSession = Guid.Empty;

            timerService.WarningTriggered += ev => warnings.Add(ev);
            timerService.ExpirationTriggered += id => expiredSession = id;

            // Use reflection to access inner tracked sessions list and adjust the start time back to simulate elapsed time
            var field = typeof(SessionTimerService).GetField("_trackedSessions", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);

            var trackedSessions = field.GetValue(timerService) as System.Collections.Concurrent.ConcurrentDictionary<Guid, SessionTimerService.TimerState>;
            Assert.NotNull(trackedSessions);
            Assert.True(trackedSessions.TryGetValue(sessionId, out var timerStateObj));

            var startTimeField = timerStateObj.GetType().GetProperty("StartTime", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(startTimeField);

            var method = typeof(SessionTimerService).GetMethod("OnTimerTick", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            // 1. Simulate 6 seconds elapsed (4 seconds remaining -> Should trigger Warning 1)
            startTimeField.SetValue(timerStateObj, DateTime.UtcNow - TimeSpan.FromSeconds(6));
            method.Invoke(timerService, new object[] { null! });

            Assert.Single(warnings);
            Assert.Equal(1, warnings[0].WarningLevel);
            Assert.Equal(sessionId, warnings[0].SessionId);

            // 2. Simulate 9 seconds elapsed (1 second remaining -> Should trigger Warning 2)
            startTimeField.SetValue(timerStateObj, DateTime.UtcNow - TimeSpan.FromSeconds(9));
            method.Invoke(timerService, new object[] { null! });

            Assert.Equal(2, warnings.Count);
            Assert.Equal(2, warnings[1].WarningLevel);

            // 3. Simulate 11 seconds elapsed (-1 second remaining -> Should trigger Expiration)
            startTimeField.SetValue(timerStateObj, DateTime.UtcNow - TimeSpan.FromSeconds(11));
            method.Invoke(timerService, new object[] { null! });

            Assert.Equal(sessionId, expiredSession);
        }

        #endregion

        #region Session Manager Tests

        [Fact]
        public async Task SessionManager_CreateStartPauseResumeCompleteCancel_ShouldWorkAndRaiseEvents()
        {
            var stateManager = new RuntimeStateManager(_stateManagerLoggerMock.Object, _eventPublisher);
            var sessionManager = new RuntimeSessionManager(_sessionManagerLoggerMock.Object, _eventPublisher, stateManager, _sessionRepository);

            List<object> raisedEvents = new();
            _eventPublisher.Subscribe<SessionCreatedEvent>(ev => raisedEvents.Add(ev));
            _eventPublisher.Subscribe<SessionStartedEvent>(ev => raisedEvents.Add(ev));
            _eventPublisher.Subscribe<SessionCompletedEvent>(ev => raisedEvents.Add(ev));
            _eventPublisher.Subscribe<SessionFailedEvent>(ev => raisedEvents.Add(ev));

            // 1. Create
            var session = await sessionManager.CreateAsync("gamer_777", "Valorant");
            Assert.NotNull(session);
            Assert.Equal("gamer_777", session.UserId);
            Assert.Equal("Valorant", session.GameId);
            Assert.Equal(RuntimeState.Created, session.Status);

            var persistedSession = await _sessionRepository.GetAsync(session.SessionId);
            Assert.NotNull(persistedSession);
            Assert.Equal(RuntimeState.Created, persistedSession.Status);

            // 2. Start (Preparing -> Starting first manually to maintain state machine rules)
            sessionManager.UpdateSessionState(session.SessionId, RuntimeState.Starting);
            await sessionManager.StartAsync(session.SessionId);
            Assert.Equal(RuntimeState.Running, session.Status);

            // 3. Pause
            await sessionManager.PauseAsync(session.SessionId);
            Assert.Equal(RuntimeState.Paused, session.Status);

            // 4. Resume
            await sessionManager.ResumeAsync(session.SessionId);
            Assert.Equal(RuntimeState.Running, session.Status);

            // 5. Complete
            await sessionManager.CompleteAsync(session.SessionId);
            Assert.Equal(RuntimeState.Completed, session.Status);

            // Verify raised events
            Assert.Contains(raisedEvents, e => e is SessionCreatedEvent sc && sc.SessionId == session.SessionId);
            Assert.Contains(raisedEvents, e => e is SessionStartedEvent ss && ss.SessionId == session.SessionId);
            Assert.Contains(raisedEvents, e => e is SessionCompletedEvent sc && sc.SessionId == session.SessionId);

            // 6. Test Cancel with a new session and fresh managers to comply with non-recyclable FSM state transitions
            var stateManager2 = new RuntimeStateManager(_stateManagerLoggerMock.Object, _eventPublisher);
            var sessionManager2 = new RuntimeSessionManager(_sessionManagerLoggerMock.Object, _eventPublisher, stateManager2, _sessionRepository);
            var session2 = await sessionManager2.CreateAsync("gamer_888", "Minecraft");
            sessionManager2.UpdateSessionState(session2.SessionId, RuntimeState.Starting);
            sessionManager2.UpdateSessionState(session2.SessionId, RuntimeState.Running);

            await sessionManager2.CancelAsync(session2.SessionId);
            Assert.Equal(RuntimeState.Failed, session2.Status);
            Assert.Contains(raisedEvents, e => e is SessionFailedEvent sf && sf.SessionId == session2.SessionId);
        }

        #endregion

        #region Expiration Handler Tests

        [Fact]
        public async Task ExpirationHandler_ShouldTriggerProcessSupervisorStop_AndStopSession()
        {
            var stateManager = new RuntimeStateManager(_stateManagerLoggerMock.Object, _eventPublisher);
            var sessionManager = new RuntimeSessionManager(_sessionManagerLoggerMock.Object, _eventPublisher, stateManager, _sessionRepository);

            var session = await sessionManager.CreateAsync("gamer_999", "ApexLegends");
            sessionManager.UpdateSessionState(session.SessionId, RuntimeState.Starting);
            sessionManager.UpdateSessionState(session.SessionId, RuntimeState.Running);

            var expirationHandler = new SessionExpirationHandler(
                _expirationHandlerLoggerMock.Object,
                sessionManager,
                _processSupervisorMock.Object,
                _eventPublisher,
                stateManager
            );

            bool expiredEventRaised = false;
            _eventPublisher.Subscribe<SessionExpiredEvent>(ev => {
                if (ev.SessionId == session.SessionId) expiredEventRaised = true;
            });

            // Handle Expiration
            await expirationHandler.HandleExpirationAsync(session.SessionId);

            // Verify state transitions to Expired and eventually to Completed
            Assert.Equal(RuntimeState.Completed, session.Status);
            Assert.True(expiredEventRaised);

            // Verify process supervisor was called to stop cleanly
            _processSupervisorMock.Verify(ps => ps.StopAsync(session.SessionId), Times.Once);
        }

        #endregion

        #region Idle Detection Tests

        [Fact]
        public void IdleDetection_ShouldChangeState_AndResetOnActivity()
        {
            var idleService = new IdleDetectionService();

            bool eventFired = false;
            bool expectedIdleState = false;

            idleService.IdleStateChanged += isIdle => {
                eventFired = true;
                expectedIdleState = isIdle;
            };

            // Threshold is 10 minutes. Simulating 5 minutes should NOT trigger idle.
            idleService.SimulateInactivity(TimeSpan.FromMinutes(5));
            Assert.False(idleService.IsIdle);
            Assert.False(eventFired);

            // Simulating 10 minutes SHOULD trigger idle.
            idleService.SimulateInactivity(TimeSpan.FromMinutes(10));
            Assert.True(idleService.IsIdle);
            Assert.True(eventFired);
            Assert.True(expectedIdleState);

            // Resetting activity should clear idle.
            eventFired = false;
            idleService.ResetActivity();
            Assert.False(idleService.IsIdle);
            Assert.Equal(TimeSpan.Zero, idleService.IdleDuration);
            Assert.True(eventFired);
            Assert.False(expectedIdleState);
        }

        #endregion

        #region End-To-End Integration Test

        [Fact]
        public async Task E2E_RuntimeSessionLifecycle_FullSequence()
        {
            // Setup services
            var stateManager = new RuntimeStateManager(_stateManagerLoggerMock.Object, _eventPublisher);
            var sessionManager = new RuntimeSessionManager(_sessionManagerLoggerMock.Object, _eventPublisher, stateManager, _sessionRepository);
            using var timerService = new SessionTimerService(_timerServiceLoggerMock.Object);
            var expirationHandler = new SessionExpirationHandler(_expirationHandlerLoggerMock.Object, sessionManager, _processSupervisorMock.Object, _eventPublisher, stateManager);

            // Capture all published events
            var capturedEvents = new List<object>();
            _eventPublisher.Subscribe<SessionCreatedEvent>(ev => capturedEvents.Add(ev));
            _eventPublisher.Subscribe<SessionStartedEvent>(ev => capturedEvents.Add(ev));
            _eventPublisher.Subscribe<SessionWarningEvent>(ev => capturedEvents.Add(ev));
            _eventPublisher.Subscribe<SessionExpiredEvent>(ev => capturedEvents.Add(ev));
            _eventPublisher.Subscribe<SessionCompletedEvent>(ev => capturedEvents.Add(ev));

            // Wire timer service expiration event to expiration handler
            timerService.ExpirationTriggered += async id =>
            {
                await expirationHandler.HandleExpirationAsync(id);
            };

            // Hook warning events to republish on the event aggregator
            timerService.WarningTriggered += ev =>
            {
                _eventPublisher.Publish(ev);
            };

            // 1. Create Session
            var session = await sessionManager.CreateAsync("gamer_pro", "CounterStrike2");
            Assert.NotNull(session);
            Assert.Equal(RuntimeState.Preparing, stateManager.CurrentState);
            Assert.Single(capturedEvents);
            Assert.IsType<SessionCreatedEvent>(capturedEvents[0]);

            // 2. Start Session (transitioning Preparing -> Starting first)
            sessionManager.UpdateSessionState(session.SessionId, RuntimeState.Starting);
            await sessionManager.StartAsync(session.SessionId);
            Assert.Equal(RuntimeState.Running, stateManager.CurrentState);
            Assert.Contains(capturedEvents, ev => ev is SessionStartedEvent);

            // 3. Timer Execution
            // Total time: 10 seconds. Warning threshold 1: 5s. Warning threshold 2: 2s.
            timerService.StartTracking(session.SessionId, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));

            // Get access to inner timer details
            var field = typeof(SessionTimerService).GetField("_trackedSessions", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            var trackedSessions = field.GetValue(timerService) as System.Collections.Concurrent.ConcurrentDictionary<Guid, SessionTimerService.TimerState>;
            Assert.NotNull(trackedSessions);
            Assert.True(trackedSessions.TryGetValue(session.SessionId, out var timerStateObj));

            var startTimeProperty = timerStateObj.GetType().GetProperty("StartTime", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(startTimeProperty);

            var tickMethod = typeof(SessionTimerService).GetMethod("OnTimerTick", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(tickMethod);

            // 4. Warning Event (Simulate 6 seconds elapsed -> 4 seconds remaining)
            startTimeProperty.SetValue(timerStateObj, DateTime.UtcNow - TimeSpan.FromSeconds(6));
            tickMethod.Invoke(timerService, new object[] { null! });

            Assert.Contains(capturedEvents, ev => ev is SessionWarningEvent swe && swe.WarningLevel == 1);

            // 5. Expiration Event & Expiration Handler Processing (Simulate 11 seconds elapsed -> expired)
            startTimeProperty.SetValue(timerStateObj, DateTime.UtcNow - TimeSpan.FromSeconds(11));
            tickMethod.Invoke(timerService, new object[] { null! });

            // 6. ProcessSupervisor.StopAsync() called (via mock verification)
            _processSupervisorMock.Verify(ps => ps.StopAsync(session.SessionId), Times.Once);

            // 7. Session Completed
            Assert.Equal(RuntimeState.Completed, stateManager.CurrentState);
            Assert.Equal(RuntimeState.Completed, session.Status);

            Assert.Contains(capturedEvents, ev => ev is SessionExpiredEvent);
            Assert.Contains(capturedEvents, ev => ev is SessionCompletedEvent);
        }

        #endregion
    }
}
