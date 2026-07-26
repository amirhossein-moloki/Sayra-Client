using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Domain.Events;
using Sayra.Client.Shared.Runtime.Domain.States;
using Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Overlay.Application.Services;
using Sayra.Client.Shared.Runtime.Overlay.Domain.Models;
using Sayra.Client.Shared.Runtime.Overlay.Domain.States;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    public class OverlayTests
    {
        private readonly Mock<ILogger<OverlayStateMachine>> _stateMachineLoggerMock = new();
        private readonly Mock<ILogger<OverlayDataProvider>> _dataProviderLoggerMock = new();
        private readonly Mock<ILogger<OverlayManager>> _managerLoggerMock = new();
        private readonly Mock<IRuntimeEventPublisher> _eventPublisherMock = new();
        private readonly Mock<IOverlayWindowService> _windowServiceMock = new();

        #region State Machine Tests

        [Fact]
        public void StateMachine_ValidTransitions_ShouldSucceed()
        {
            var stateMachine = new OverlayStateMachine(_stateMachineLoggerMock.Object);
            Assert.Equal(OverlayState.Hidden, stateMachine.CurrentState);

            stateMachine.TransitionTo(OverlayState.Initializing);
            Assert.Equal(OverlayState.Initializing, stateMachine.CurrentState);

            stateMachine.TransitionTo(OverlayState.Visible);
            Assert.Equal(OverlayState.Visible, stateMachine.CurrentState);

            stateMachine.TransitionTo(OverlayState.Updating);
            Assert.Equal(OverlayState.Updating, stateMachine.CurrentState);

            stateMachine.TransitionTo(OverlayState.Visible);
            Assert.Equal(OverlayState.Visible, stateMachine.CurrentState);

            stateMachine.TransitionTo(OverlayState.Closing);
            Assert.Equal(OverlayState.Closing, stateMachine.CurrentState);

            stateMachine.TransitionTo(OverlayState.Hidden);
            Assert.Equal(OverlayState.Hidden, stateMachine.CurrentState);

            stateMachine.TransitionTo(OverlayState.Disposed);
            Assert.Equal(OverlayState.Disposed, stateMachine.CurrentState);
        }

        [Fact]
        public void StateMachine_InvalidTransitions_ShouldThrowException()
        {
            var stateMachine = new OverlayStateMachine(_stateMachineLoggerMock.Object);

            // From Hidden to Updating is invalid
            Assert.Throws<InvalidOperationException>(() => stateMachine.TransitionTo(OverlayState.Updating));

            // From Hidden to Closing is invalid
            Assert.Throws<InvalidOperationException>(() => stateMachine.TransitionTo(OverlayState.Closing));

            // From Hidden to Disposed is valid, let's transition there to test terminal state block
            stateMachine.TransitionTo(OverlayState.Disposed);

            // Cannot transition from Disposed
            Assert.Throws<InvalidOperationException>(() => stateMachine.TransitionTo(OverlayState.Hidden));
        }

        #endregion

        #region Data Provider Tests

        [Fact]
        public void DataProvider_ConvertSessionStartedEvent_ShouldSetCorrectData()
        {
            var provider = new OverlayDataProvider(_dataProviderLoggerMock.Object, null);
            var sessionId = Guid.NewGuid();
            var userId = "test_player";

            OverlayData? updatedData = null;
            provider.DataUpdated += data => updatedData = data;

            provider.HandleSessionStarted(sessionId, userId);

            Assert.NotNull(updatedData);
            Assert.Equal(sessionId, updatedData.SessionId);
            Assert.Equal("Running", updatedData.SessionState);
            Assert.Equal(0, updatedData.WarningLevel);
            Assert.True(updatedData.Visibility);
            Assert.Contains(userId, updatedData.Message);
        }

        [Fact]
        public void DataProvider_ConvertSessionWarningEvent_ShouldSetCorrectData()
        {
            var provider = new OverlayDataProvider(_dataProviderLoggerMock.Object, null);
            var sessionId = Guid.NewGuid();
            var userId = "test_player";
            var remainingTime = TimeSpan.FromMinutes(10);
            var warningLevel = 1;
            var message = "10 minutes remaining!";

            OverlayData? updatedData = null;
            provider.DataUpdated += data => updatedData = data;

            provider.HandleSessionWarning(sessionId, userId, remainingTime, warningLevel, message);

            Assert.NotNull(updatedData);
            Assert.Equal(sessionId, updatedData.SessionId);
            Assert.Equal("Warning", updatedData.SessionState);
            Assert.Equal(warningLevel, updatedData.WarningLevel);
            Assert.Equal(remainingTime, updatedData.RemainingTime);
            Assert.Equal(message, updatedData.Message);
            Assert.True(updatedData.Visibility);
        }

        [Fact]
        public void DataProvider_ConvertSessionExpiredEvent_ShouldSetCorrectData()
        {
            var provider = new OverlayDataProvider(_dataProviderLoggerMock.Object, null);
            var sessionId = Guid.NewGuid();

            OverlayData? updatedData = null;
            provider.DataUpdated += data => updatedData = data;

            provider.HandleSessionExpired(sessionId);

            Assert.NotNull(updatedData);
            Assert.Equal(sessionId, updatedData.SessionId);
            Assert.Equal("Expired", updatedData.SessionState);
            Assert.Equal(3, updatedData.WarningLevel);
            Assert.Equal(TimeSpan.Zero, updatedData.RemainingTime);
            Assert.True(updatedData.Visibility);
        }

        [Fact]
        public void DataProvider_ConvertSessionCompletedEvent_ShouldSetCorrectData()
        {
            var provider = new OverlayDataProvider(_dataProviderLoggerMock.Object, null);
            var sessionId = Guid.NewGuid();

            OverlayData? updatedData = null;
            provider.DataUpdated += data => updatedData = data;

            provider.HandleSessionCompleted(sessionId);

            Assert.NotNull(updatedData);
            Assert.Equal(sessionId, updatedData.SessionId);
            Assert.Equal("Completed", updatedData.SessionState);
            Assert.Equal(0, updatedData.WarningLevel);
            Assert.False(updatedData.Visibility);
        }

        #endregion

        #region Overlay Manager Tests

        [Fact]
        public async Task OverlayManager_Show_ShouldTransitionAndCallWindowService()
        {
            var dataProviderMock = new Mock<IOverlayDataProvider>();
            var manager = new OverlayManager(_managerLoggerMock.Object, dataProviderMock.Object, _windowServiceMock.Object);

            Assert.Equal(OverlayState.Hidden, manager.StateMachine.CurrentState);

            await manager.ShowAsync();

            Assert.Equal(OverlayState.Visible, manager.StateMachine.CurrentState);
            _windowServiceMock.Verify(ws => ws.ShowWindowAsync(), Times.Once);
        }

        [Fact]
        public async Task OverlayManager_Hide_ShouldTransitionAndCallWindowService()
        {
            var dataProviderMock = new Mock<IOverlayDataProvider>();
            var manager = new OverlayManager(_managerLoggerMock.Object, dataProviderMock.Object, _windowServiceMock.Object);

            // Pre-condition: Show
            await manager.ShowAsync();
            Assert.Equal(OverlayState.Visible, manager.StateMachine.CurrentState);

            await manager.HideAsync();

            Assert.Equal(OverlayState.Hidden, manager.StateMachine.CurrentState);
            _windowServiceMock.Verify(ws => ws.HideWindowAsync(), Times.Once);
        }

        [Fact]
        public async Task OverlayManager_Update_ShouldTransitionAndCallWindowService()
        {
            var dataProviderMock = new Mock<IOverlayDataProvider>();
            var manager = new OverlayManager(_managerLoggerMock.Object, dataProviderMock.Object, _windowServiceMock.Object);
            var testData = new OverlayData { SessionId = Guid.NewGuid(), RemainingTime = TimeSpan.FromMinutes(5) };

            // Pre-condition: Show
            await manager.ShowAsync();

            await manager.UpdateAsync(testData);

            Assert.Equal(OverlayState.Visible, manager.StateMachine.CurrentState);
            _windowServiceMock.Verify(ws => ws.UpdateContentAsync(testData), Times.Once);
        }

        #endregion
    }
}
