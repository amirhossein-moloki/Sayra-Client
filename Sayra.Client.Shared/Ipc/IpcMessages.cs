using System;

namespace Sayra.Client.Shared.Ipc
{
    public enum IpcMessageType
    {
        // UI -> Core (Requests)
        GET_STATE,
        START_SESSION,
        STOP_SESSION,
        PAUSE_SESSION,
        RESUME_SESSION,
        LAUNCH_APP,
        KILL_APP,
        LOCK_PC,
        GET_APPS,

        // Core -> UI (Events/Responses)
        STATE_UPDATED,
        SESSION_STARTED,
        SESSION_ENDED,
        SESSION_TIME_UPDATED,
        PROCESS_STARTED,
        PROCESS_EXITED,
        CONNECTION_STATUS_CHANGED,
        COMMAND_RESPONSE,
        APPS_LIST
    }

    public class IpcMessage
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public IpcMessageType MessageType { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Payload { get; set; } // JSON serialized payload
    }

    public class IpcCommandResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Result { get; set; }
    }

    public class LaunchAppRequest
    {
        public string AppId { get; set; } = string.Empty;
    }

    public class KillAppRequest
    {
        public string ProcessName { get; set; } = string.Empty;
    }
}
