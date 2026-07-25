using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Domain.States;
using Sayra.Client.Shared.Runtime.Domain.Entities;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Launch.Application.Services;
using Sayra.Client.Shared.Runtime.Launch.Domain.Models;
using Sayra.Client.Shared.Runtime.Launch.Domain.Exceptions;
using Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;
using Sayra.Client.Shared.Security.GameProtection.Domain.Models;
using Sayra.Client.Shared.Security.GameProtection.Domain.Rules;

namespace Sayra.Client.Configuration.Tests
{
    public class SecureLaunchTests
    {
        private readonly Mock<ILogger<SecureLauncher>> _loggerMock = new();
        private readonly Mock<IRuntimeEventPublisher> _eventPublisherMock = new();
        private readonly Mock<IRuntimeSessionManager> _sessionManagerMock = new();
        private readonly Mock<IRuntimeStateManager> _stateManagerMock = new();
        private readonly Mock<ILaunchProfileProvider> _profileProviderMock = new();
        private readonly Mock<IUserSessionProvider> _sessionProviderMock = new();
        private readonly Mock<IProcessCreator> _processCreatorMock = new();
        private readonly Mock<Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces.IProcessSupervisor> _processSupervisorMock = new();

        [Fact]
        public async Task LaunchAsync_ValidExecutable_ShouldSucceed()
        {
            // Arrange
            var gameId = "game_123";
            var tempFile = Path.GetTempFileName();
            var request = new LaunchRequest
            {
                GameId = gameId,
                ExecutablePath = tempFile,
                Arguments = "--play",
                WorkingDirectory = Path.GetDirectoryName(tempFile) ?? string.Empty,
                UserId = "user_999",
                RuntimeSessionId = Guid.NewGuid()
            };

            var profile = new LaunchProfile { GameId = gameId, ExecutablePath = tempFile };
            _profileProviderMock.Setup(x => x.GetProfileAsync(gameId)).ReturnsAsync(profile);

            _sessionProviderMock.Setup(x => x.GetActiveSessionAsync()).ReturnsAsync(new UserSessionInfo
            {
                SessionId = 1,
                Username = "RestrictedUser",
                IsInteractive = true
            });

            _processCreatorMock.Setup(x => x.CreateProcessAsync(request, profile, 1))
                .ReturnsAsync(new LaunchResult { Success = true, ProcessId = 1234 });

            var validator = new LaunchValidator(new Mock<ILogger<LaunchValidator>>().Object);
            var launcher = new SecureLauncher(
                _loggerMock.Object,
                _eventPublisherMock.Object,
                _sessionManagerMock.Object,
                _stateManagerMock.Object,
                _profileProviderMock.Object,
                validator,
                _sessionProviderMock.Object,
                _processCreatorMock.Object,
                _processSupervisorMock.Object
            );

            // Act
            var result = await launcher.LaunchAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1234, result.ProcessId);

            _stateManagerMock.Verify(x => x.TransitionTo(RuntimeState.Preparing, It.IsAny<string>()), Times.Once);
            _stateManagerMock.Verify(x => x.TransitionTo(RuntimeState.Starting, It.IsAny<string>()), Times.Once);
            _stateManagerMock.Verify(x => x.TransitionTo(RuntimeState.Running, It.IsAny<string>()), Times.Once);
            _sessionManagerMock.Verify(x => x.UpdateSessionState(request.RuntimeSessionId, RuntimeState.Running), Times.Once);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task LaunchAsync_MissingExecutable_ShouldThrowLaunchValidationException()
        {
            // Arrange
            var gameId = "game_123";
            var request = new LaunchRequest
            {
                GameId = gameId,
                ExecutablePath = "C:\\Games\\NonExistent.exe",
                Arguments = "--play",
                RuntimeSessionId = Guid.NewGuid()
            };

            var profile = new LaunchProfile { GameId = gameId };
            _profileProviderMock.Setup(x => x.GetProfileAsync(gameId)).ReturnsAsync(profile);

            var validator = new LaunchValidator(new Mock<ILogger<LaunchValidator>>().Object);
            var launcher = new SecureLauncher(
                _loggerMock.Object,
                _eventPublisherMock.Object,
                _sessionManagerMock.Object,
                _stateManagerMock.Object,
                _profileProviderMock.Object,
                validator,
                _sessionProviderMock.Object,
                _processCreatorMock.Object,
                _processSupervisorMock.Object
            );

            // Act
            var result = await launcher.LaunchAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.ErrorMessage);
            _stateManagerMock.Verify(x => x.TransitionTo(RuntimeState.Failed, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task LaunchAsync_InvalidPolicyDecision_ShouldBeBlocked()
        {
            // Arrange
            var gameId = "game_123";
            var tempFile = Path.GetTempFileName();
            var request = new LaunchRequest
            {
                GameId = gameId,
                ExecutablePath = tempFile,
                RuntimeSessionId = Guid.NewGuid()
            };

            var profile = new LaunchProfile { GameId = gameId, ExecutablePath = tempFile };
            _profileProviderMock.Setup(x => x.GetProfileAsync(gameId)).ReturnsAsync(profile);

            // Mock policy evaluator and integrity validator
            var integrityMock = new Mock<IIntegrityValidator>();
            integrityMock.Setup(x => x.ValidateExecutable(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new IntegrityResult { Status = IntegrityStatus.Valid });

            var policyMock = new Mock<IProcessPolicyEvaluator>();
            policyMock.Setup(x => x.Evaluate(It.IsAny<ProcessInfo>())).Returns(new SecurityDecision
            {
                Action = ProcessAction.Block,
                Reason = "Process is blacklisted"
            });

            var validator = new LaunchValidator(new Mock<ILogger<LaunchValidator>>().Object, integrityMock.Object, policyMock.Object);
            var launcher = new SecureLauncher(
                _loggerMock.Object,
                _eventPublisherMock.Object,
                _sessionManagerMock.Object,
                _stateManagerMock.Object,
                _profileProviderMock.Object,
                validator,
                _sessionProviderMock.Object,
                _processCreatorMock.Object,
                _processSupervisorMock.Object
            );

            // Act
            var result = await launcher.LaunchAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("rejected by Track 4.6 security policy", result.ErrorMessage);
            _stateManagerMock.Verify(x => x.TransitionTo(RuntimeState.Failed, It.IsAny<string>()), Times.Once);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task LaunchAsync_UserSessionUnavailable_ShouldFailAndThrowCorrectException()
        {
            // Arrange
            var gameId = "game_123";
            var tempFile = Path.GetTempFileName();
            var request = new LaunchRequest
            {
                GameId = gameId,
                ExecutablePath = tempFile,
                RuntimeSessionId = Guid.NewGuid()
            };

            var profile = new LaunchProfile { GameId = gameId, ExecutablePath = tempFile };
            _profileProviderMock.Setup(x => x.GetProfileAsync(gameId)).ReturnsAsync(profile);

            _sessionProviderMock.Setup(x => x.GetActiveSessionAsync())
                .ThrowsAsync(new UserSessionUnavailableException("No active console session found."));

            var validator = new LaunchValidator(new Mock<ILogger<LaunchValidator>>().Object);
            var launcher = new SecureLauncher(
                _loggerMock.Object,
                _eventPublisherMock.Object,
                _sessionManagerMock.Object,
                _stateManagerMock.Object,
                _profileProviderMock.Object,
                validator,
                _sessionProviderMock.Object,
                _processCreatorMock.Object,
                _processSupervisorMock.Object
            );

            // Act
            var result = await launcher.LaunchAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("No active console session found", result.ErrorMessage);
            _stateManagerMock.Verify(x => x.TransitionTo(RuntimeState.Failed, It.IsAny<string>()), Times.Once);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public async Task LaunchAsync_ProcessSpawningFailure_ShouldTransitionToFailed()
        {
            // Arrange
            var gameId = "game_123";
            var tempFile = Path.GetTempFileName();
            var request = new LaunchRequest
            {
                GameId = gameId,
                ExecutablePath = tempFile,
                RuntimeSessionId = Guid.NewGuid()
            };

            var profile = new LaunchProfile { GameId = gameId, ExecutablePath = tempFile };
            _profileProviderMock.Setup(x => x.GetProfileAsync(gameId)).ReturnsAsync(profile);

            _sessionProviderMock.Setup(x => x.GetActiveSessionAsync()).ReturnsAsync(new UserSessionInfo
            {
                SessionId = 1,
                Username = "RestrictedUser",
                IsInteractive = true
            });

            _processCreatorMock.Setup(x => x.CreateProcessAsync(request, profile, 1))
                .ReturnsAsync(new LaunchResult { Success = false, ErrorMessage = "Win32 Error 5: Access Denied" });

            var validator = new LaunchValidator(new Mock<ILogger<LaunchValidator>>().Object);
            var launcher = new SecureLauncher(
                _loggerMock.Object,
                _eventPublisherMock.Object,
                _sessionManagerMock.Object,
                _stateManagerMock.Object,
                _profileProviderMock.Object,
                validator,
                _sessionProviderMock.Object,
                _processCreatorMock.Object,
                _processSupervisorMock.Object
            );

            // Act
            var result = await launcher.LaunchAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Access Denied", result.ErrorMessage);
            _stateManagerMock.Verify(x => x.TransitionTo(RuntimeState.Failed, It.IsAny<string>()), Times.Once);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
