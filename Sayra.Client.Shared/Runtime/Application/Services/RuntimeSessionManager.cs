using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Domain.Entities;
using Sayra.Client.Shared.Runtime.Domain.Events;
using Sayra.Client.Shared.Runtime.Domain.States;
using Sayra.Client.Shared.Runtime.Domain.Exceptions;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Infrastructure.Persistence;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;

namespace Sayra.Client.Shared.Runtime.Application.Services
{
    public class RuntimeSessionManager : IRuntimeSessionManager
    {
        private readonly ILogger<RuntimeSessionManager> _logger;
        private readonly IRuntimeEventPublisher _eventPublisher;
        private readonly IRuntimeStateManager _stateManager;
        private readonly ISessionRepository _sessionRepository;
        private readonly ISandboxManager? _sandboxManager;
        private readonly IRegistryVirtualizationManager? _registryManager;
        private readonly ILaunchProfileProvider? _profileProvider;
        private readonly ConcurrentDictionary<Guid, RuntimeSession> _sessions = new();

        public RuntimeSessionManager(
            ILogger<RuntimeSessionManager> logger,
            IRuntimeEventPublisher eventPublisher,
            IRuntimeStateManager stateManager,
            ISessionRepository sessionRepository)
            : this(logger, eventPublisher, stateManager, sessionRepository, null, null, null)
        {
        }

        public RuntimeSessionManager(
            ILogger<RuntimeSessionManager> logger,
            IRuntimeEventPublisher eventPublisher,
            IRuntimeStateManager stateManager,
            ISessionRepository sessionRepository,
            ISandboxManager? sandboxManager,
            IRegistryVirtualizationManager? registryManager,
            ILaunchProfileProvider? profileProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            _sandboxManager = sandboxManager;
            _registryManager = registryManager;
            _profileProvider = profileProvider;
        }

        // Backward compatible constructor for tests
        public RuntimeSessionManager(
            ILogger<RuntimeSessionManager> logger,
            IRuntimeEventPublisher eventPublisher,
            IRuntimeStateManager stateManager)
            : this(logger, eventPublisher, stateManager, new InMemorySessionRepository(), null, null, null)
        {
        }

        public Task<RuntimeSession> CreateAsync()
        {
            return CreateAsync("DefaultUser", "DefaultGame");
        }

        public async Task<RuntimeSession> CreateAsync(string userId, string gameId)
        {
            var session = new RuntimeSession
            {
                SessionId = Guid.NewGuid(),
                UserId = userId,
                GameId = gameId,
                StartTime = DateTime.UtcNow,
                Status = RuntimeState.Created,
                RuntimeState = RuntimeState.Created
            };

            if (!_sessions.TryAdd(session.SessionId, session))
            {
                throw new RuntimeException("Failed to register runtime session.");
            }

            // Persist
            await _sessionRepository.SaveAsync(session);

            _logger.LogInformation("Runtime session created. SessionId: {SessionId}, UserId: {UserId}, GameId: {GameId}", session.SessionId, userId, gameId);

            // Publish legacy event
            _eventPublisher.Publish(new RuntimeSessionCreatedEvent(session));

            // Publish new SessionCreatedEvent
            _eventPublisher.Publish(new SessionCreatedEvent(session.SessionId, userId, RuntimeState.Created, $"Session created for game {gameId}"));

            _stateManager.TransitionTo(RuntimeState.Preparing, $"Session created for user {userId}");
            await _sessionRepository.SaveAsync(session);

            return session;
        }

        public async Task StartAsync(Guid sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new RuntimeException($"Session {sessionId} not found.");
            }

            _stateManager.TransitionTo(RuntimeState.Running, $"Starting session {sessionId}");
            session.Status = RuntimeState.Running;
            session.RuntimeState = RuntimeState.Running;

            await _sessionRepository.SaveAsync(session);

            _eventPublisher.Publish(new SessionStartedEvent(sessionId, session.UserId, RuntimeState.Running, "Session started."));
            _logger.LogInformation("Runtime session started. SessionId: {SessionId}", sessionId);
        }

        public async Task PauseAsync(Guid sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new RuntimeException($"Session {sessionId} not found.");
            }

            _stateManager.TransitionTo(RuntimeState.Paused, $"Pausing session {sessionId}");
            session.Status = RuntimeState.Paused;
            session.RuntimeState = RuntimeState.Paused;

            await _sessionRepository.SaveAsync(session);
            _logger.LogInformation("Runtime session paused. SessionId: {SessionId}", sessionId);
        }

        public async Task ResumeAsync(Guid sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new RuntimeException($"Session {sessionId} not found.");
            }

            _stateManager.TransitionTo(RuntimeState.Running, $"Resuming session {sessionId}");
            session.Status = RuntimeState.Running;
            session.RuntimeState = RuntimeState.Running;

            await _sessionRepository.SaveAsync(session);
            _logger.LogInformation("Runtime session resumed. SessionId: {SessionId}", sessionId);
        }

        private async Task CleanupSessionResourcesAsync(RuntimeSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.GameId)) return;

            if (_profileProvider != null)
            {
                try
                {
                    var profile = await _profileProvider.GetProfileAsync(session.GameId);
                    if (profile != null)
                    {
                        if (_sandboxManager != null && !string.IsNullOrWhiteSpace(profile.SandboxPath))
                        {
                            _logger.LogInformation("Guaranteed resource cleanup: Cleaning up sandbox path '{SandboxPath}'", profile.SandboxPath);
                            await _sandboxManager.CleanupSandboxAsync(session.GameId, profile.SandboxPath);
                        }

                        if (_registryManager != null && profile.VirtualRegistryKeys != null && profile.VirtualRegistryKeys.Count > 0)
                        {
                            _logger.LogInformation("Guaranteed resource cleanup: Cleaning up virtualized registry keys.");
                            await _registryManager.CleanupRegistryAsync(session.SessionId, session.GameId, profile.VirtualRegistryKeys);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cleanly dispose resources for Game {GameId} on Session {SessionId}", session.GameId, session.SessionId);
                }
            }
        }

        public async Task CompleteAsync(Guid sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new RuntimeException($"Session {sessionId} not found.");
            }

            _stateManager.TransitionTo(RuntimeState.Stopping, $"Stopping session {sessionId} for completion");
            _stateManager.TransitionTo(RuntimeState.Completed, $"Completing session {sessionId}");
            session.EndTime = DateTime.UtcNow;
            session.Status = RuntimeState.Completed;
            session.RuntimeState = RuntimeState.Completed;

            await CleanupSessionResourcesAsync(session);

            await _sessionRepository.SaveAsync(session);

            _eventPublisher.Publish(new SessionCompletedEvent(sessionId, session.UserId, RuntimeState.Completed, "Session completed successfully."));
            _logger.LogInformation("Runtime session completed. SessionId: {SessionId}", sessionId);
        }

        public async Task CancelAsync(Guid sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new RuntimeException($"Session {sessionId} not found.");
            }

            _stateManager.TransitionTo(RuntimeState.Failed, $"Cancelling session {sessionId}");
            session.EndTime = DateTime.UtcNow;
            session.Status = RuntimeState.Failed;
            session.RuntimeState = RuntimeState.Failed;

            await CleanupSessionResourcesAsync(session);

            await _sessionRepository.SaveAsync(session);

            _eventPublisher.Publish(new SessionFailedEvent(sessionId, session.UserId, RuntimeState.Failed, "Session was cancelled."));
            _logger.LogInformation("Runtime session cancelled. SessionId: {SessionId}", sessionId);
        }

        public async Task StopAsync(Guid sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                _logger.LogWarning("Session not found for stopping: {SessionId}", sessionId);
                throw new RuntimeException($"Session {sessionId} not found.");
            }

            if (_stateManager.CurrentState != RuntimeState.Stopping)
            {
                _stateManager.TransitionTo(RuntimeState.Stopping, $"Request to stop session {sessionId}");
            }
            session.EndTime = DateTime.UtcNow;
            session.Status = RuntimeState.Completed;
            session.RuntimeState = RuntimeState.Completed;

            await CleanupSessionResourcesAsync(session);

            _stateManager.TransitionTo(RuntimeState.Completed, $"Session {sessionId} completed successfully.");
            await _sessionRepository.SaveAsync(session);

            _eventPublisher.Publish(new SessionCompletedEvent(sessionId, session.UserId, RuntimeState.Completed, "Session stopped and completed."));
            _logger.LogInformation("Runtime session stopped. SessionId: {SessionId}", sessionId);
        }

        public RuntimeSession? GetSession(Guid sessionId)
        {
            _sessions.TryGetValue(sessionId, out var session);
            return session;
        }

        public void UpdateSessionState(Guid sessionId, RuntimeState state)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new RuntimeException($"Session {sessionId} not found.");
            }

            _stateManager.TransitionTo(state, $"Session state update to {state}");
            session.Status = state;
            session.RuntimeState = state;

            _sessionRepository.SaveAsync(session).GetAwaiter().GetResult();

            if (state == RuntimeState.Failed)
            {
                _eventPublisher.Publish(new SessionFailedEvent(sessionId, session.UserId, state, "Session encountered a failure state."));
            }
        }
    }
}
