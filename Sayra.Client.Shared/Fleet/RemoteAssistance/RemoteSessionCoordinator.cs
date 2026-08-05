using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;
using Sayra.Client.Shared.Models.Phase9.Events;

namespace Sayra.Client.Shared.Fleet.RemoteAssistance
{
    /// <summary>
    /// Thread-safe coordinator for remote support session state transitions, recording handles, and automated session timeout loops.
    /// </summary>
    public class RemoteSessionCoordinator : IDisposable
    {
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<RemoteSessionCoordinator> _logger;

        private readonly ConcurrentDictionary<string, RemoteSession> _sessions = new();
        private readonly ConcurrentDictionary<string, RemoteSessionRequest> _requests = new();
        private readonly ConcurrentDictionary<string, SessionRecording> _recordings = new();
        private readonly ConcurrentDictionary<string, DateTime> _sessionActivity = new();

        private readonly Timer _timeoutCheckTimer;
        private readonly TimeSpan _sessionMaxLifetime = TimeSpan.FromHours(1);
        private readonly TimeSpan _sessionIdleTimeout = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Initializes a new instance of RemoteSessionCoordinator.
        /// </summary>
        public RemoteSessionCoordinator(
            IEventDispatcher eventDispatcher,
            ILogger<RemoteSessionCoordinator> logger)
        {
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Automated timeout checker loop executing every 10 seconds
            _timeoutCheckTimer = new Timer(CheckSessionTimeouts, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Registers a new session connection request.
        /// </summary>
        public bool RegisterRequest(RemoteSessionRequest request)
        {
            if (request == null || !request.Validate()) return false;
            _requests[request.RequestId] = request;
            _logger.LogInformation("Support request registered: ID={Id}, Machine={Machine}, Operator={Operator}",
                request.RequestId, request.MachineId, request.AdministratorId);
            return true;
        }

        /// <summary>
        /// Gets an existing session request by ID.
        /// </summary>
        public RemoteSessionRequest? GetRequest(string requestId)
        {
            _requests.TryGetValue(requestId, out var req);
            return req;
        }

        /// <summary>
        /// Approves a session request and transitions it to Approved state.
        /// </summary>
        public bool ApproveRequest(string requestId, string approvedByOperatorId)
        {
            if (!_requests.TryGetValue(requestId, out var request))
            {
                _logger.LogWarning("Cannot approve non-existent session request '{Id}'", requestId);
                return false;
            }

            var session = new RemoteSession
            {
                SessionId = requestId,
                TargetMachineId = request.MachineId,
                ConnectionType = SupportSessionType.UnifiedRemoteSupport,
                Status = RemoteSessionStatus.Approved,
                AllowedPermissions = SupportPermission.InteractiveExecution,
                Participants = new List<RemoteSessionParticipant>
                {
                    new()
                    {
                        ParticipantId = request.AdministratorId,
                        FriendlyName = $"Operator {request.AdministratorId}",
                        Role = "Administrator",
                        JoinedAtUtc = DateTime.UtcNow
                    }
                },
                CreatedAtUtc = DateTime.UtcNow
            };

            _sessions[requestId] = session;
            _sessionActivity[requestId] = DateTime.UtcNow;

            _logger.LogInformation("Support Session '{Id}' APPROVED by '{OperatorId}'", requestId, approvedByOperatorId);
            _eventDispatcher.Dispatch(new RemoteSessionApproved(requestId, request.MachineId, approvedByOperatorId));

            return true;
        }

        /// <summary>
        /// Rejects a session request and transitions it to Rejected state.
        /// </summary>
        public bool RejectRequest(string requestId, string rejectReason)
        {
            if (!_requests.TryGetValue(requestId, out var request)) return false;

            _logger.LogInformation("Support Request '{Id}' REJECTED: {Reason}", requestId, rejectReason);
            _eventDispatcher.Dispatch(new RemoteSessionRejected(requestId, request.MachineId, rejectReason));
            _requests.TryRemove(requestId, out _);

            return true;
        }

        /// <summary>
        /// Opens/Starts the visual streaming connection.
        /// </summary>
        public bool OpenSession(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return false;

            var updated = session with { Status = RemoteSessionStatus.Active };
            _sessions[sessionId] = updated;
            _sessionActivity[sessionId] = DateTime.UtcNow;

            _logger.LogInformation("Support Session '{Id}' is now ACTIVE", sessionId);
            _eventDispatcher.Dispatch(new RemoteSessionStarted(sessionId, session.TargetMachineId, session.ConnectionType));

            return true;
        }

        /// <summary>
        /// Closes and cleans up an active session.
        /// </summary>
        public bool CloseSession(string sessionId, RemoteSessionStatus terminationStatus = RemoteSessionStatus.Ended)
        {
            if (!_sessions.TryRemove(sessionId, out var session)) return false;

            _sessionActivity.TryRemove(sessionId, out _);
            _requests.TryRemove(sessionId, out _);

            _logger.LogInformation("Support Session '{Id}' TERMINATED with status: {Status}", sessionId, terminationStatus);
            _eventDispatcher.Dispatch(new RemoteSessionEnded(sessionId, session.TargetMachineId, terminationStatus));

            return true;
        }

        /// <summary>
        /// Pauses an active support session.
        /// </summary>
        public bool PauseSession(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return false;

            var updated = session with { Status = RemoteSessionStatus.Paused };
            _sessions[sessionId] = updated;
            _sessionActivity[sessionId] = DateTime.UtcNow;

            _logger.LogInformation("Support Session '{Id}' PAUSED", sessionId);
            return true;
        }

        /// <summary>
        /// Resumes a paused support session.
        /// </summary>
        public bool ResumeSession(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return false;

            var updated = session with { Status = RemoteSessionStatus.Active };
            _sessions[sessionId] = updated;
            _sessionActivity[sessionId] = DateTime.UtcNow;

            _logger.LogInformation("Support Session '{Id}' RESUMED", sessionId);
            return true;
        }

        /// <summary>
        /// Registers a participant to a support session.
        /// </summary>
        public bool AddParticipant(string sessionId, RemoteSessionParticipant participant)
        {
            if (participant == null || string.IsNullOrEmpty(sessionId)) return false;

            if (_sessions.TryGetValue(sessionId, out var session))
            {
                var list = session.Participants.ToList();
                list.Add(participant);
                _sessions[sessionId] = session with { Participants = list };
                _sessionActivity[sessionId] = DateTime.UtcNow;
                _logger.LogInformation("Participant '{Name}' joined session '{SessionId}'", participant.FriendlyName, sessionId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Registers a session recording handle metadata.
        /// </summary>
        public void RegisterRecording(SessionRecording recording)
        {
            if (recording == null) return;
            _recordings[recording.SessionId] = recording;
            _logger.LogInformation("Session recording registered for '{SessionId}': File={File}, Size={Size}B",
                recording.SessionId, recording.FilePath, recording.RecordedBytes);
        }

        /// <summary>
        /// Retrieves recording metadata for a session.
        /// </summary>
        public SessionRecording? GetRecording(string sessionId)
        {
            _recordings.TryGetValue(sessionId, out var rec);
            return rec;
        }

        /// <summary>
        /// Updates the last active timestamp of a session (preventing idle timeouts).
        /// </summary>
        public void KeepAlive(string sessionId)
        {
            _sessionActivity[sessionId] = DateTime.UtcNow;
        }

        /// <summary>
        /// Retrieves a session by its unique ID.
        /// </summary>
        public RemoteSession? GetSession(string sessionId)
        {
            _sessions.TryGetValue(sessionId, out var session);
            return session;
        }

        /// <summary>
        /// Retrieves all active remote support sessions.
        /// </summary>
        public IReadOnlyList<RemoteSession> GetAllSessions()
        {
            return _sessions.Values.ToList();
        }

        private void CheckSessionTimeouts(object? state)
        {
            var now = DateTime.UtcNow;

            foreach (var kvp in _sessionActivity.ToList())
            {
                var sessionId = kvp.Key;
                var lastSeen = kvp.Value;

                if (!_sessions.TryGetValue(sessionId, out var session)) continue;

                var lifetime = now.Subtract(session.CreatedAtUtc);
                var idleTime = now.Subtract(lastSeen);

                if (lifetime > _sessionMaxLifetime)
                {
                    _logger.LogWarning("Session '{SessionId}' exceeded maximum lifetime of {Limit} minutes. Force closing.",
                        sessionId, _sessionMaxLifetime.TotalMinutes);
                    CloseSession(sessionId, RemoteSessionStatus.Disconnected);
                }
                else if (idleTime > _sessionIdleTimeout)
                {
                    _logger.LogWarning("Session '{SessionId}' was idle for more than {Limit} minutes. Force closing.",
                        sessionId, _sessionIdleTimeout.TotalMinutes);
                    CloseSession(sessionId, RemoteSessionStatus.Disconnected);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _timeoutCheckTimer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
