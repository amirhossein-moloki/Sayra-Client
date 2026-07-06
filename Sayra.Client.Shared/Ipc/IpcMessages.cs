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
        GET_RUNNING_GAMES,

        // Core -> UI (Events/Responses)
        STATE_UPDATED,
        SESSION_STARTED,
        SESSION_ENDED,
        SESSION_TIME_UPDATED,
        PROCESS_STARTED,
        PROCESS_EXITED,
        GAME_LAUNCHED,
        GAME_EXITED,
        GAME_FAILED,
        PROCESS_KILLED,
        CONNECTION_STATUS_CHANGED,
        COMMAND_RESPONSE,
        APPS_LIST,
        BILLING_UPDATE,

        // Update Events
        UPDATE_AVAILABLE,
        UPDATE_STARTED,
        UPDATE_PROGRESS,
        UPDATE_SUCCESS,
        UPDATE_FAILED,
        UPDATE_ROLLBACK,

        // Security Events
        SECURITY_BREACH_DETECTED,
        PROCESS_BLOCKED,
        KIOSK_POLICY_REAPPLIED,
        DEBUGGER_DETECTED,
        SERVICE_TAMPER_ATTEMPT
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
        public string GameId { get; set; } = string.Empty;
    }

    public class ProcessEventPayload
    {
        public int Pid { get; set; }
        public string GameId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class KillAppRequest
    {
        public int? Pid { get; set; }
        public string? GameId { get; set; }
        public string? ProcessName { get; set; }
    }
}
