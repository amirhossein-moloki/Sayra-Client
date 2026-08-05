using System;
using System.Collections.Generic;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Models.Phase9.Domain
{
    /// <summary>
    /// Lifecycle states of an enterprise remote assistance support session.
    /// </summary>
    public enum RemoteSessionState
    {
        /// <summary>
        /// Remote support session has been requested.
        /// </summary>
        Requested,

        /// <summary>
        /// Request has been authorized and approved by the target workstation or policies.
        /// </summary>
        Approved,

        /// <summary>
        /// Connection negotiation and handshake are in progress.
        /// </summary>
        Connecting,

        /// <summary>
        /// Session is active and streaming visual and control telemetry.
        /// </summary>
        Active,

        /// <summary>
        /// Streaming is temporarily suspended.
        /// </summary>
        Paused,

        /// <summary>
        /// Session completed and resources were cleanly deallocated.
        /// </summary>
        Completed,

        /// <summary>
        /// Request was rejected or unauthorized.
        /// </summary>
        Rejected,

        /// <summary>
        /// Connection request expired before handshake finished.
        /// </summary>
        Expired
    }

    /// <summary>
    /// Immutable record representing a remote session connection request.
    /// </summary>
    public record RemoteSessionRequest
    {
        /// <summary>
        /// Gets the unique session request identifier.
        /// </summary>
        public string RequestId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the administrative operator identity requesting access.
        /// </summary>
        public string AdministratorId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the targeted workstation machine identifier.
        /// </summary>
        public string MachineId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the purpose or justification for requesting control.
        /// </summary>
        public string Reason { get; init; } = string.Empty;

        /// <summary>
        /// Gets the timestamp when the request was generated.
        /// </summary>
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Validates that the request has required information.
        /// </summary>
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(RequestId) &&
                   !string.IsNullOrWhiteSpace(AdministratorId) &&
                   !string.IsNullOrWhiteSpace(MachineId) &&
                   !string.IsNullOrWhiteSpace(Reason);
        }
    }

    /// <summary>
    /// Immutable record representing an approval response for a remote support request.
    /// </summary>
    public record RemoteSessionApproval
    {
        /// <summary>
        /// Gets the targeted session request identifier.
        /// </summary>
        public string RequestId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the identity of the administrator/user approving the session.
        /// </summary>
        public string ApprovedByOperatorId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the resulting state of the session approval process.
        /// </summary>
        public RemoteSessionState State { get; init; } = RemoteSessionState.Approved;

        /// <summary>
        /// Gets any specific reasons or constraints applied to the approval.
        /// </summary>
        public string Notes { get; init; } = string.Empty;

        /// <summary>
        /// Gets the timestamp of approval.
        /// </summary>
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Immutable record representing low-latency visual, input, and encryption configuration parameters.
    /// </summary>
    public record RemoteSessionMetadata
    {
        /// <summary>
        /// Gets the remote session identifier.
        /// </summary>
        public string SessionId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the symmetric AES/ChaCha20 session key used to encrypt the visuals and input channels.
        /// </summary>
        public string SessionEncryptionKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets whether low latency optimization mode is enabled for poor connection environments.
        /// </summary>
        public bool LowLatencyModeEnabled { get; init; } = true;

        /// <summary>
        /// Gets the frames per second limit of visual stream.
        /// </summary>
        public int FrameRateLimit { get; init; } = 30;
    }

    /// <summary>
    /// Immutable record representing the metadata for a recorded remote support session.
    /// </summary>
    public record SessionRecording
    {
        /// <summary>
        /// Gets the recorded session identifier.
        /// </summary>
        public string SessionId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the path to the recorded visual stream archive file.
        /// </summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets the total size in bytes of the recording file.
        /// </summary>
        public long RecordedBytes { get; init; }

        /// <summary>
        /// Gets the duration of the recording.
        /// </summary>
        public TimeSpan Duration { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the identity of the operator who recorded the session.
        /// </summary>
        public string OperatorId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the recording timestamp.
        /// </summary>
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    }
}
