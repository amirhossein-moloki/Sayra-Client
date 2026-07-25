using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Domain.Entities;
using Sayra.Client.Shared.Runtime.Domain.Events;
using Sayra.Client.Shared.Runtime.Domain.States;
using Sayra.Client.Shared.Runtime.Domain.Exceptions;
using Sayra.Client.Shared.Runtime.Application.Interfaces;

namespace Sayra.Client.Shared.Runtime.Application.Services
{
    public class RuntimeSessionManager : IRuntimeSessionManager
    {
        private readonly ILogger<RuntimeSessionManager> _logger;
        private readonly IRuntimeEventPublisher _eventPublisher;
        private readonly IRuntimeStateManager _stateManager;
        private readonly ConcurrentDictionary<Guid, RuntimeSession> _sessions = new();

        public RuntimeSessionManager(
            ILogger<RuntimeSessionManager> logger,
            IRuntimeEventPublisher eventPublisher,
            IRuntimeStateManager stateManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        }

        public Task<RuntimeSession> CreateAsync()
        {
            return CreateAsync("DefaultUser", "DefaultGame");
        }

        public Task<RuntimeSession> CreateAsync(string userId, string gameId)
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

            _logger.LogInformation("Runtime session created. SessionId: {SessionId}, UserId: {UserId}, GameId: {GameId}", session.SessionId, userId, gameId);
            _eventPublisher.Publish(new RuntimeSessionCreatedEvent(session));

            _stateManager.TransitionTo(RuntimeState.Preparing, $"Session created for user {userId}");

            return Task.FromResult(session);
        }

        public Task StopAsync(Guid sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                _logger.LogWarning("Session not found for stopping: {SessionId}", sessionId);
                throw new RuntimeException($"Session {sessionId} not found.");
            }

            _stateManager.TransitionTo(RuntimeState.Stopping, $"Request to stop session {sessionId}");
            session.EndTime = DateTime.UtcNow;
            session.Status = RuntimeState.Completed;
            session.RuntimeState = RuntimeState.Completed;

            _stateManager.TransitionTo(RuntimeState.Completed, $"Session {sessionId} completed successfully.");
            _logger.LogInformation("Runtime session stopped. SessionId: {SessionId}", sessionId);

            return Task.CompletedTask;
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
        }
    }
}
