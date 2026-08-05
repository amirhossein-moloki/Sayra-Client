using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Interfaces.Phase9
{
    /// <summary>
    /// Decoupled abstraction for high-performance visual screen streaming and input channel handling.
    /// </summary>
    public interface IRemoteDesktopProvider
    {
        /// <summary>
        /// Initiates and retrieves the visual screen frame stream for a remote session.
        /// </summary>
        Task<Stream> GetScreenCaptureStreamAsync(string sessionId, CancellationToken ct = default);

        /// <summary>
        /// Dispatches administrative mouse or keyboard input actions into the active session.
        /// </summary>
        Task SendInputEventAsync(string sessionId, string inputPayloadJson, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the current connection state of the desktop provider.
        /// </summary>
        Task<RemoteSessionState> GetSessionStateAsync(string sessionId, CancellationToken ct = default);
    }

    /// <summary>
    /// Decoupled abstraction for executing remote terminal commands with strict session isolation and permission checking.
    /// </summary>
    public interface IRemoteConsoleService
    {
        /// <summary>
        /// Sends a command to the remote interactive terminal channel.
        /// </summary>
        Task ExecuteConsoleCommandAsync(string sessionId, string command, CancellationToken ct = default);

        /// <summary>
        /// Streams real-time text output from the remote interactive terminal channel.
        /// </summary>
        IAsyncEnumerable<string> GetConsoleOutputStreamAsync(string sessionId, CancellationToken ct = default);

        /// <summary>
        /// Validates if an operator is authorized to execute a specific command on a target workstation.
        /// </summary>
        Task<bool> ValidatePermissionsAsync(string operatorId, string machineId, string command, CancellationToken ct = default);
    }

    /// <summary>
    /// Decoupled abstraction for real-time remote log streaming, querying, and export metadata generation.
    /// </summary>
    public interface IRemoteLogStreamService
    {
        /// <summary>
        /// Streams live workstation log entries with optional text and severity filters.
        /// </summary>
        IAsyncEnumerable<string> StreamLogsAsync(
            string sessionId,
            string? filter = null,
            NotificationSeverity? minSeverity = null,
            CancellationToken ct = default);

        /// <summary>
        /// Builds and exports a structured diagnostic metadata package about the session's log activity.
        /// </summary>
        Task<string> ExportLogMetadataAsync(string sessionId, CancellationToken ct = default);
    }

    /// <summary>
    /// Decoupled abstraction for live telemetry, system, and application-level event streams.
    /// </summary>
    public interface IRemoteEventStreamService
    {
        /// <summary>
        /// Subscribes to the live event stream for a remote session.
        /// </summary>
        IAsyncEnumerable<string> StreamEventsAsync(string sessionId, CancellationToken ct = default);

        /// <summary>
        /// Manually dispatches/publishes an event JSON payload to the session's stream subscribers.
        /// </summary>
        Task PublishEventAsync(string sessionId, string eventJson, CancellationToken ct = default);
    }
}
