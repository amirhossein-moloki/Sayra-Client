using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Domain.Events;
using Sayra.Client.Shared.Runtime.Domain.States;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;

namespace Sayra.Client.Shared.Runtime.Application.Services
{
    public class SessionExpirationHandler : ISessionExpirationHandler
    {
        private readonly ILogger<SessionExpirationHandler> _logger;
        private readonly IRuntimeSessionManager _sessionManager;
        private readonly IProcessSupervisor _processSupervisor;
        private readonly IRuntimeEventPublisher _eventPublisher;
        private readonly IRuntimeStateManager _stateManager;
        private readonly ISandboxManager? _sandboxManager;
        private readonly IRegistryVirtualizationManager? _registryManager;
        private readonly ILaunchProfileProvider? _profileProvider;

        // Concurrent tracking dictionary to ensure absolute idempotency
        private readonly ConcurrentDictionary<Guid, byte> _expiredSessions = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        public SessionExpirationHandler(
            ILogger<SessionExpirationHandler> logger,
            IRuntimeSessionManager sessionManager,
            IProcessSupervisor processSupervisor,
            IRuntimeEventPublisher eventPublisher,
            IRuntimeStateManager stateManager)
            : this(logger, sessionManager, processSupervisor, eventPublisher, stateManager, null, null, null)
        {
        }

        public SessionExpirationHandler(
            ILogger<SessionExpirationHandler> logger,
            IRuntimeSessionManager sessionManager,
            IProcessSupervisor processSupervisor,
            IRuntimeEventPublisher eventPublisher,
            IRuntimeStateManager stateManager,
            ISandboxManager? sandboxManager,
            IRegistryVirtualizationManager? registryManager,
            Sayra.Client.Shared.Runtime.Launch.Application.Interfaces.ILaunchProfileProvider? profileProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _processSupervisor = processSupervisor ?? throw new ArgumentNullException(nameof(processSupervisor));
            _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _sandboxManager = sandboxManager;
            _registryManager = registryManager;
            _profileProvider = profileProvider;
        }

        public async Task HandleExpirationAsync(Guid sessionId)
        {
            // Rapid double-check (non-blocking)
            if (_expiredSessions.ContainsKey(sessionId))
            {
                _logger.LogWarning("Session {SessionId} has already expired or is undergoing expiration processing. Aborting duplicate execution.", sessionId);
                return;
            }

            await _lock.WaitAsync();
            try
            {
                // Double-checked locking pattern inside lock
                if (!_expiredSessions.TryAdd(sessionId, 0))
                {
                    _logger.LogWarning("Session {SessionId} already processed inside lock. Aborting.", sessionId);
                    return;
                }

                _logger.LogInformation("Processing expiration sequence for Session: {SessionId}", sessionId);

                var session = _sessionManager.GetSession(sessionId);
                string userId = session?.UserId ?? "Unknown";
                string gameId = session?.GameId ?? string.Empty;

                // 1. Publish SessionExpiredEvent
                _eventPublisher.Publish(new SessionExpiredEvent(sessionId, userId, RuntimeState.Expired, "Session has expired due to playtime limit."));

                // Clean up session sandbox and registry keys on expiration
                if (!string.IsNullOrEmpty(gameId))
                {
                    try
                    {
                        if (_profileProvider != null)
                        {
                            var profile = await _profileProvider.GetProfileAsync(gameId);
                            if (profile != null)
                            {
                                if (_sandboxManager != null && !string.IsNullOrWhiteSpace(profile.SandboxPath))
                                {
                                    _logger.LogInformation("Cleaning up sandbox path '{SandboxPath}' on session expiration.", profile.SandboxPath);
                                    await _sandboxManager.CleanupSandboxAsync(gameId, profile.SandboxPath);
                                }

                                if (_registryManager != null && profile.VirtualRegistryKeys != null && profile.VirtualRegistryKeys.Count > 0)
                                {
                                    _logger.LogInformation("Cleaning up virtualized registry keys on session expiration.");
                                    await _registryManager.CleanupRegistryAsync(sessionId, gameId, profile.VirtualRegistryKeys);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to perform resource cleanup during expiration for Game '{GameId}' on session {SessionId}.", gameId, sessionId);
                    }
                }

                // 2. Transition state machine to Expired
                _stateManager.TransitionTo(RuntimeState.Expired, "Session expired");

                // 3. Notify Process Supervisor to stop the running games/subprocesses safely (Track 4.3)
                try
                {
                    await _processSupervisor.StopAsync(sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to stop processes cleanly through Process Supervisor on session {SessionId}.", sessionId);
                }

                // 4. Update and Complete the session via Session Manager
                try
                {
                    await _sessionManager.StopAsync(sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to stop session inside Session Manager on session {SessionId}.", sessionId);
                }

                _logger.LogInformation("Expiration sequence completed successfully for Session: {SessionId}", sessionId);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
