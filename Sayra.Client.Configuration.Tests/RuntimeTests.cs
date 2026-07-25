using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.Shared.Runtime.Domain.Entities;
using Sayra.Client.Shared.Runtime.Domain.Events;
using Sayra.Client.Shared.Runtime.Domain.Exceptions;
using Sayra.Client.Shared.Runtime.Domain.States;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Application.Services;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    public class RuntimeTests
    {
        private readonly Mock<ILogger<RuntimeStateManager>> _stateManagerLoggerMock;
        private readonly Mock<ILogger<RuntimeSessionManager>> _sessionManagerLoggerMock;
        private readonly IRuntimeEventPublisher _eventPublisher;
        private readonly IRuntimeStateManager _stateManager;
        private readonly IRuntimeSessionManager _sessionManager;
        private readonly IRuntimeContextProvider _contextProvider;

        public RuntimeTests()
        {
            _stateManagerLoggerMock = new Mock<ILogger<RuntimeStateManager>>();
            _sessionManagerLoggerMock = new Mock<ILogger<RuntimeSessionManager>>();
            _eventPublisher = new RuntimeEventPublisher(eventDispatcher: null);
            _stateManager = new RuntimeStateManager(_stateManagerLoggerMock.Object, _eventPublisher);
            _sessionManager = new RuntimeSessionManager(_sessionManagerLoggerMock.Object, _eventPublisher, _stateManager);
            _contextProvider = new RuntimeContextProvider();
        }

        #region Runtime State Machine Tests

        [Fact]
        public void StateMachine_InitialState_IsCreated()
        {
            Assert.Equal(RuntimeState.Created, _stateManager.CurrentState);
        }

        [Fact]
        public void StateMachine_ValidTransitionSequence_Works()
        {
            // Transition: Created -> Preparing -> Starting -> Running -> Stopping -> Completed
            _stateManager.TransitionTo(RuntimeState.Preparing);
            Assert.Equal(RuntimeState.Preparing, _stateManager.CurrentState);

            _stateManager.TransitionTo(RuntimeState.Starting);
            Assert.Equal(RuntimeState.Starting, _stateManager.CurrentState);

            _stateManager.TransitionTo(RuntimeState.Running);
            Assert.Equal(RuntimeState.Running, _stateManager.CurrentState);

            _stateManager.TransitionTo(RuntimeState.Stopping);
            Assert.Equal(RuntimeState.Stopping, _stateManager.CurrentState);

            _stateManager.TransitionTo(RuntimeState.Completed);
            Assert.Equal(RuntimeState.Completed, _stateManager.CurrentState);
        }

        [Theory]
        [InlineData(RuntimeState.Created, RuntimeState.Running)]
        [InlineData(RuntimeState.Created, RuntimeState.Stopping)]
        [InlineData(RuntimeState.Created, RuntimeState.Completed)]
        [InlineData(RuntimeState.Preparing, RuntimeState.Running)]
        [InlineData(RuntimeState.Preparing, RuntimeState.Completed)]
        [InlineData(RuntimeState.Running, RuntimeState.Created)]
        [InlineData(RuntimeState.Completed, RuntimeState.Running)]
        [InlineData(RuntimeState.Failed, RuntimeState.Created)]
        public void StateMachine_InvalidTransition_ThrowsRuntimeTransitionException(RuntimeState fromState, RuntimeState toState)
        {
            // Force state manager into specified starting state
            if (fromState != RuntimeState.Created)
            {
                // Navigate state machine to the starting state if possible, or bypass using reflection if necessary.
                // Since our transitions are strictly hierarchical:
                // Created -> Preparing -> Starting -> Running -> Paused / Stopping -> Completed / Failed
                if (fromState == RuntimeState.Preparing)
                {
                    _stateManager.TransitionTo(RuntimeState.Preparing);
                }
                else if (fromState == RuntimeState.Starting)
                {
                    _stateManager.TransitionTo(RuntimeState.Preparing);
                    _stateManager.TransitionTo(RuntimeState.Starting);
                }
                else if (fromState == RuntimeState.Running)
                {
                    _stateManager.TransitionTo(RuntimeState.Preparing);
                    _stateManager.TransitionTo(RuntimeState.Starting);
                    _stateManager.TransitionTo(RuntimeState.Running);
                }
                else if (fromState == RuntimeState.Paused)
                {
                    _stateManager.TransitionTo(RuntimeState.Preparing);
                    _stateManager.TransitionTo(RuntimeState.Starting);
                    _stateManager.TransitionTo(RuntimeState.Running);
                    _stateManager.TransitionTo(RuntimeState.Paused);
                }
                else if (fromState == RuntimeState.Stopping)
                {
                    _stateManager.TransitionTo(RuntimeState.Preparing);
                    _stateManager.TransitionTo(RuntimeState.Starting);
                    _stateManager.TransitionTo(RuntimeState.Running);
                    _stateManager.TransitionTo(RuntimeState.Stopping);
                }
                else if (fromState == RuntimeState.Completed)
                {
                    _stateManager.TransitionTo(RuntimeState.Preparing);
                    _stateManager.TransitionTo(RuntimeState.Starting);
                    _stateManager.TransitionTo(RuntimeState.Running);
                    _stateManager.TransitionTo(RuntimeState.Stopping);
                    _stateManager.TransitionTo(RuntimeState.Completed);
                }
                else if (fromState == RuntimeState.Failed)
                {
                    _stateManager.TransitionTo(RuntimeState.Failed);
                }
            }

            Assert.Throws<RuntimeTransitionException>(() => _stateManager.TransitionTo(toState));
        }

        [Theory]
        [InlineData(RuntimeState.Created)]
        [InlineData(RuntimeState.Preparing)]
        [InlineData(RuntimeState.Starting)]
        [InlineData(RuntimeState.Running)]
        [InlineData(RuntimeState.Paused)]
        [InlineData(RuntimeState.Stopping)]
        public void StateMachine_TransitionToFailed_IsAllowedFromAnyState(RuntimeState startingState)
        {
            if (startingState != RuntimeState.Created)
            {
                if (startingState == RuntimeState.Preparing)
                {
                    _stateManager.TransitionTo(RuntimeState.Preparing);
                }
                else if (startingState == RuntimeState.Starting)
                {
                    _stateManager.TransitionTo(RuntimeState.Preparing);
                    _stateManager.TransitionTo(RuntimeState.Starting);
                }
                else if (startingState == RuntimeState.Running)
                {
                    _stateManager.TransitionTo(RuntimeState.Preparing);
                    _stateManager.TransitionTo(RuntimeState.Starting);
                    _stateManager.TransitionTo(RuntimeState.Running);
                }
                else if (startingState == RuntimeState.Paused)
                {
                    _stateManager.TransitionTo(RuntimeState.Preparing);
                    _stateManager.TransitionTo(RuntimeState.Starting);
                    _stateManager.TransitionTo(RuntimeState.Running);
                    _stateManager.TransitionTo(RuntimeState.Paused);
                }
                else if (startingState == RuntimeState.Stopping)
                {
                    _stateManager.TransitionTo(RuntimeState.Preparing);
                    _stateManager.TransitionTo(RuntimeState.Starting);
                    _stateManager.TransitionTo(RuntimeState.Running);
                    _stateManager.TransitionTo(RuntimeState.Stopping);
                }
            }

            _stateManager.TransitionTo(RuntimeState.Failed, "Testing failure path");
            Assert.Equal(RuntimeState.Failed, _stateManager.CurrentState);
        }

        #endregion

        #region Runtime Session Tests

        [Fact]
        public async Task SessionManager_CreateAsync_CreatesSessionAndSetsCorrectStates()
        {
            var userId = "Gamer_1337";
            var gameId = "DOTA_2";

            RuntimeSessionCreatedEvent? receivedEvent = null;
            _eventPublisher.Subscribe<RuntimeSessionCreatedEvent>(ev => receivedEvent = ev);

            var session = await _sessionManager.CreateAsync(userId, gameId);

            Assert.NotNull(session);
            Assert.NotEqual(Guid.Empty, session.SessionId);
            Assert.Equal(userId, session.UserId);
            Assert.Equal(gameId, session.GameId);
            Assert.Equal(RuntimeState.Created, session.Status);
            Assert.Equal(RuntimeState.Created, session.RuntimeState);
            Assert.True(session.StartTime <= DateTime.UtcNow);
            Assert.Null(session.EndTime);

            // Verifies state manager transitioned to Preparing
            Assert.Equal(RuntimeState.Preparing, _stateManager.CurrentState);

            // Verifies session created event was dispatched
            Assert.NotNull(receivedEvent);
            Assert.Equal(session.SessionId, receivedEvent.Session.SessionId);
        }

        [Fact]
        public async Task SessionManager_UpdateSessionState_UpdatesAndTransitionsCorrectly()
        {
            var session = await _sessionManager.CreateAsync("User", "Game");

            // State manager is at Preparing now. Transition it to Starting.
            _sessionManager.UpdateSessionState(session.SessionId, RuntimeState.Starting);
            Assert.Equal(RuntimeState.Starting, session.Status);
            Assert.Equal(RuntimeState.Starting, session.RuntimeState);
            Assert.Equal(RuntimeState.Starting, _stateManager.CurrentState);

            // Transition session to Running.
            _sessionManager.UpdateSessionState(session.SessionId, RuntimeState.Running);
            Assert.Equal(RuntimeState.Running, session.Status);
            Assert.Equal(RuntimeState.Running, _stateManager.CurrentState);
        }

        [Fact]
        public async Task SessionManager_StopAsync_CompletesSessionSuccessfully()
        {
            var session = await _sessionManager.CreateAsync("User", "Game");

            // Current state of state manager is Preparing. Transition to Starting -> Running.
            _sessionManager.UpdateSessionState(session.SessionId, RuntimeState.Starting);
            _sessionManager.UpdateSessionState(session.SessionId, RuntimeState.Running);

            // Stop session
            await _sessionManager.StopAsync(session.SessionId);

            Assert.NotNull(session.EndTime);
            Assert.Equal(RuntimeState.Completed, session.Status);
            Assert.Equal(RuntimeState.Completed, session.RuntimeState);
            Assert.Equal(RuntimeState.Completed, _stateManager.CurrentState);
        }

        #endregion

        #region Events System Tests

        [Fact]
        public void EventPublisher_DirectSubscribers_AreNotifiedOfSpecificEvents()
        {
            RuntimeStartedEvent? startedEvent = null;
            RuntimeStoppedEvent? stoppedEvent = null;
            RuntimeFailedEvent? failedEvent = null;
            RuntimeStateChangedEvent? stateChangedEvent = null;

            _eventPublisher.Subscribe<RuntimeStartedEvent>(ev => startedEvent = ev);
            _eventPublisher.Subscribe<RuntimeStoppedEvent>(ev => stoppedEvent = ev);
            _eventPublisher.Subscribe<RuntimeFailedEvent>(ev => failedEvent = ev);
            _eventPublisher.Subscribe<RuntimeStateChangedEvent>(ev => stateChangedEvent = ev);

            // 1. Trigger transition Created -> Preparing (state changed event)
            _stateManager.TransitionTo(RuntimeState.Preparing, "Prep");
            Assert.NotNull(stateChangedEvent);
            Assert.Equal(RuntimeState.Created, stateChangedEvent.OldState);
            Assert.Equal(RuntimeState.Preparing, stateChangedEvent.NewState);
            Assert.Equal("Prep", stateChangedEvent.Reason);

            // Reset stateChangedEvent to verify next one
            stateChangedEvent = null;

            // 2. Transition Preparing -> Starting
            _stateManager.TransitionTo(RuntimeState.Starting);

            // 3. Transition Starting -> Running (triggers started event & state changed event)
            _stateManager.TransitionTo(RuntimeState.Running, "Game active");
            Assert.NotNull(startedEvent);
            Assert.Equal("Game active", startedEvent.Reason);
            Assert.NotNull(stateChangedEvent);
            Assert.Equal(RuntimeState.Starting, stateChangedEvent.OldState);
            Assert.Equal(RuntimeState.Running, stateChangedEvent.NewState);

            // Reset stateChangedEvent
            stateChangedEvent = null;

            // 4. Transition Running -> Stopping
            _stateManager.TransitionTo(RuntimeState.Stopping);

            // 5. Transition Stopping -> Completed (triggers stopped event)
            _stateManager.TransitionTo(RuntimeState.Completed, "Session done");
            Assert.NotNull(stoppedEvent);
            Assert.Equal("Session done", stoppedEvent.Reason);
        }

        [Fact]
        public void EventPublisher_SubscriberThrowsException_DoesNotCrashPublisher()
        {
            var callCount = 0;
            _eventPublisher.Subscribe<RuntimeStartedEvent>(ev => throw new Exception("Subscribers should not crash publisher!"));
            _eventPublisher.Subscribe<RuntimeStartedEvent>(ev => callCount++);

            // Publishing should succeed and not throw
            var exception = Record.Exception(() => _eventPublisher.Publish(new RuntimeStartedEvent("Test robust")));
            Assert.Null(exception);
            Assert.Equal(1, callCount);
        }

        #endregion

        #region Context Provider Tests

        [Fact]
        public void ContextProvider_ProvidesDefaultContext_IfNoneSet()
        {
            var context = _contextProvider.GetContext();
            Assert.NotNull(context);
            Assert.Equal("DefaultGame", context.GameIdentifier);
            Assert.Null(context.ProcessId);
        }

        [Fact]
        public void ContextProvider_SavesAndRetrievesSetContextCorrectly()
        {
            var expectedContext = new GameRuntimeContext
            {
                GameIdentifier = "GTA_V",
                ExecutablePath = "C:\\Games\\GTA V\\GTAV.exe",
                ProcessId = 9876,
                SessionId = Guid.NewGuid(),
                LaunchArguments = "-fullscreen"
            };

            _contextProvider.SetContext(expectedContext);

            var retrieved = _contextProvider.GetContext();
            Assert.NotNull(retrieved);
            Assert.Equal(expectedContext.GameIdentifier, retrieved.GameIdentifier);
            Assert.Equal(expectedContext.ExecutablePath, retrieved.ExecutablePath);
            Assert.Equal(expectedContext.ProcessId, retrieved.ProcessId);
            Assert.Equal(expectedContext.SessionId, retrieved.SessionId);
            Assert.Equal(expectedContext.LaunchArguments, retrieved.LaunchArguments);
        }

        #endregion
    }
}
