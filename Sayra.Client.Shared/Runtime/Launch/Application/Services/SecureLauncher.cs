using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Domain.States;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Launch.Domain.Models;
using Sayra.Client.Shared.Runtime.Launch.Domain.Events;
using Sayra.Client.Shared.Runtime.Launch.Domain.Exceptions;

namespace Sayra.Client.Shared.Runtime.Launch.Application.Services
{
    public class SecureLauncher : ISecureLauncher
    {
        private readonly ILogger<SecureLauncher> _logger;
        private readonly IRuntimeEventPublisher _eventPublisher;
        private readonly IRuntimeSessionManager _sessionManager;
        private readonly IRuntimeStateManager _stateManager;
        private readonly ILaunchProfileProvider _profileProvider;
        private readonly ILaunchValidator _validator;
        private readonly IUserSessionProvider _sessionProvider;
        private readonly IProcessCreator _processCreator;

        public SecureLauncher(
            ILogger<SecureLauncher> logger,
            IRuntimeEventPublisher eventPublisher,
            IRuntimeSessionManager sessionManager,
            IRuntimeStateManager stateManager,
            ILaunchProfileProvider profileProvider,
            ILaunchValidator validator,
            IUserSessionProvider sessionProvider,
            IProcessCreator processCreator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _profileProvider = profileProvider ?? throw new ArgumentNullException(nameof(profileProvider));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _processCreator = processCreator ?? throw new ArgumentNullException(nameof(processCreator));
        }

        public async Task<LaunchResult> LaunchAsync(LaunchRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Orchestrating secure launch pipeline for GameId: '{GameId}'", request.GameId);

            // 1. Publish Launch Requested Event
            _eventPublisher.Publish(new LaunchRequestedEvent(request));

            try
            {
                // 2. Transition state to Preparing
                _stateManager.TransitionTo(RuntimeState.Preparing, $"Preparing environment to launch {request.GameId}");

                // 3. Resolve launch profile
                var profile = await _profileProvider.GetProfileAsync(request.GameId);

                // 4. Validate before launch (integrating with integrity & security policies)
                await _validator.ValidateAsync(request, profile);

                // 5. Discover active user session details
                var sessionInfo = await _sessionProvider.GetActiveSessionAsync();

                // 6. Transition state to Starting
                _stateManager.TransitionTo(RuntimeState.Starting, $"Starting process for {request.GameId} under Session {sessionInfo.SessionId}");

                // 7. Spawn process via the abstracted ProcessCreator
                var launchResult = await _processCreator.CreateProcessAsync(request, profile, sessionInfo.SessionId);

                if (!launchResult.Success || !launchResult.ProcessId.HasValue)
                {
                    string error = launchResult.ErrorMessage ?? "Process creation failed without a specific error.";
                    _logger.LogError("Game launch failed. Reason: {Reason}", error);
                    _eventPublisher.Publish(new LaunchFailedEvent(request.GameId, request.RuntimeSessionId, error));
                    _stateManager.TransitionTo(RuntimeState.Failed, $"Launch failed: {error}");
                    _sessionManager.UpdateSessionState(request.RuntimeSessionId, RuntimeState.Failed);
                    return launchResult;
                }

                int pid = launchResult.ProcessId.Value;

                // 8. Launch succeeded
                _logger.LogInformation("Game launch successful. Game: '{GameId}' PID: {Pid}", request.GameId, pid);

                // Transition state to Running
                _stateManager.TransitionTo(RuntimeState.Running, $"Successfully launched process {pid}");
                _sessionManager.UpdateSessionState(request.RuntimeSessionId, RuntimeState.Running);

                // Publish Lifecycle Events
                _eventPublisher.Publish(new LaunchStartedEvent(request.GameId, request.RuntimeSessionId, pid));
                _eventPublisher.Publish(new LaunchCompletedEvent(request.GameId, request.RuntimeSessionId, pid));

                return launchResult;
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                _logger.LogError(ex, "Game launch failed. Reason: {Reason}", errorMessage);

                _eventPublisher.Publish(new LaunchFailedEvent(request.GameId, request.RuntimeSessionId, errorMessage));

                try
                {
                    _stateManager.TransitionTo(RuntimeState.Failed, $"Launch exception: {errorMessage}");
                    _sessionManager.UpdateSessionState(request.RuntimeSessionId, RuntimeState.Failed);
                }
                catch (Exception stateEx)
                {
                    _logger.LogWarning("Failed to transition session state to Failed: {Msg}", stateEx.Message);
                }

                return new LaunchResult
                {
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }
        }
    }
}
