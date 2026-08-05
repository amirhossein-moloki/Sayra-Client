using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.RemoteAssistance
{
    /// <summary>
    /// Production-grade implementation of IRemoteSessionManager coordinating session activations, shutdowns, and participant registries.
    /// </summary>
    public class RemoteSessionManager : IRemoteSessionManager
    {
        private readonly RemoteSessionCoordinator _coordinator;
        private readonly ILogger<RemoteSessionManager> _logger;

        /// <summary>
        /// Initializes a new instance of RemoteSessionManager.
        /// </summary>
        public RemoteSessionManager(
            RemoteSessionCoordinator coordinator,
            ILogger<RemoteSessionManager> logger)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<bool> OpenSessionAsync(string sessionId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sessionId)) return Task.FromResult(false);
            _logger.LogInformation("Opening Remote Support Session '{Id}'", sessionId);
            var result = _coordinator.OpenSession(sessionId);
            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task<bool> CloseSessionAsync(string sessionId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sessionId)) return Task.FromResult(false);
            _logger.LogInformation("Closing Remote Support Session '{Id}'", sessionId);
            var result = _coordinator.CloseSession(sessionId);
            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task<bool> AddParticipantAsync(string sessionId, RemoteSessionParticipant participant, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sessionId) || participant == null) return Task.FromResult(false);
            _logger.LogInformation("Adding participant '{Name}' to support session '{Id}'", participant.FriendlyName, sessionId);
            var result = _coordinator.AddParticipant(sessionId, participant);
            return Task.FromResult(result);
        }
    }
}
