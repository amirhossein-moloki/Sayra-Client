# Sayra Server - Software Architecture Document

## 1. Executive Summary
The **Sayra Server** is the central orchestration hub for the **Sayra Client** ecosystem, a LAN-based game center management system. This document details the server-side requirements derived from the feature-complete Windows Client Agent. The server must provide a high-security, low-latency environment for managing multiple client terminals, ensuring session integrity, and facilitating remote system control. The architecture follows a modular, "fail-closed" security model utilizing AES-256-CBC and HMAC-SHA256 for all command traffic.

## 2. Functional Requirements
*   **Secure Connection Orchestration:** Simultaneous management of multiple persistent TCP connections.
*   **Cryptographic Handshake:** Challenge-response authentication and per-session key rotation.
*   **Session State Authority:** Remote control and persistence of client session lifecycles (Start, Stop, Pause).
*   **Command Dispatch:** Routing of administrative actions (Process control, System power, Diagnostics).
*   **Telemetry Aggregation:** Collection and storage of real-time client performance metrics.
*   **Update Distribution:** Managed hosting of signed binary updates and manifests.
*   **State Reconciliation:** Proactive synchronization of client state upon connection or recovery.

## 3. Non-Functional Requirements
*   **Security:** Zero plaintext production traffic; 10-second timestamp tolerance for replay protection.
*   **Reliability:** Persistence of all critical states to allow recovery from server or client restarts.
*   **Performance:** Sub-100ms command latency over standard Gigabit LAN.
*   **Scalability:** Support for up to 500 concurrent clients per server instance.
*   **Portability:** Server core should be deployable on Windows or Linux environments within the LAN.

## 4. Module Requirements Analysis

### 4.1 Network Layer
*   **Client Expectations:** Persistent TCP socket; newline-delimited JSON messaging; automatic reconnection logic.
*   **Server Responsibilities:** Listen on Port 5000; manage asynchronous connection pool; handle message framing.
*   **Required APIs:** `TcpListener`, `ConnectionPoolManager`.
*   **Required Message Types:** Raw JSON strings.
*   **Required Data Models:** `ConnectionMetadata { IP, Port, ConnectedAt, SocketState }`.
*   **Validation Rules:** Enforce max message size (e.g., 1MB) to prevent DoS.
*   **Failure Scenarios:** Client socket timeout; LAN congestion.
*   **Recovery Behavior:** Graceful socket disposal; wait for client-initiated reconnect.

### 4.2 Authentication
*   **Client Expectations:** Receive `AUTH_CHALLENGE`; receive `AUTH_STATUS` after submitting response.
*   **Server Responsibilities:** Generate cryptographically strong challenges; verify HMAC-SHA256 against MasterKey; decrypt SessionKey.
*   **Required APIs:** `HandshakeSvc`, `ChallengeGenerator`.
*   **Required Message Types:** `AUTH_CHALLENGE`, `AUTH_RESPONSE`, `AUTH_STATUS`.
*   **Required Data Models:** `AuthSession { Challenge, SessionKey, Status }`.
*   **Validation Rules:** HMAC must match `HMAC(MasterKey, Challenge)`; SessionKey must be exactly 32 bytes.
*   **Failure Scenarios:** Invalid MasterKey; challenge timeout.
*   **Recovery Behavior:** Terminate connection immediately on auth failure.

### 4.3 Secure Communication
*   **Client Expectations:** Encrypted and signed envelope for all post-auth traffic.
*   **Server Responsibilities:** Decrypt AES-256-CBC; verify HMAC-SHA256; validate UTC timestamp window.
*   **Required APIs:** `SecureTransportLayer`, `CryptoEngine`.
*   **Required Message Types:** `SecureEnvelope { payload, signature, timestamp }`.
*   **Required Data Models:** `MessageContext { DecryptedPayload, IsValid, ReceiveTime }`.
*   **Validation Rules:** Signature must match `HMAC(SessionKey, timestamp|payload)`; Timestamp ±10s from server UTC.
*   **Failure Scenarios:** Corrupt signature; replay attack; decryption error.
*   **Recovery Behavior:** Silently drop invalid messages; log security violation.

### 4.4 Session Management
*   **Client Expectations:** Execute session commands; report session timeouts.
*   **Server Responsibilities:** Master of session state; track remaining time; persist session progress.
*   **Required APIs:** `SessionEngine`, `PersistenceSvc`.
*   **Required Message Types:** `START_SESSION`, `STOP_SESSION`, `PAUSE_SESSION`, `RESUME_SESSION`, `EVENT: SESSION_ENDED`.
*   **Required Data Models:** `SessionRecord { SessionId, PcId, StartTime, Duration, Status, Elapsed }`.
*   **Validation Rules:** Session status transitions must follow IDLE -> ACTIVE -> (PAUSED/ENDED).
*   **Failure Scenarios:** Client crash during ACTIVE session; network drop.
*   **Recovery Behavior:** On reconnect, push the current authoritative state to the client.

### 4.5 Command Processing
*   **Client Expectations:** Reliable execution of remote requests.
*   **Server Responsibilities:** Map commands to target PCs; correlate results with requests.
*   **Required APIs:** `CommandRouter`, `ResultAggregator`.
*   **Required Message Types:** `COMMAND`, `EXECUTION_RESULT`.
*   **Required Data Models:** `CommandPacket { Action, Payload, RequestId }`.
*   **Validation Rules:** Payload must contain required fields for the specific Action.
*   **Failure Scenarios:** Client returns ERROR status; command execution timeout.
*   **Recovery Behavior:** Log execution failure; notify admin; do not auto-retry destructive commands.

### 4.6 Process & Game Control
*   **Client Expectations:** Remote Run/Kill/List capabilities.
*   **Server Responsibilities:** Maintain database of application paths; manage process monitoring requests.
*   **Required APIs:** `AppControlSvc`.
*   **Required Message Types:** `RUN_APP`, `KILL_APP`, `LIST_PROCESSES`.
*   **Required Data Models:** `ProcessList { List of { Name, Pid } }`.
*   **Validation Rules:** `RUN_APP` paths must be validated against a whitelist if configured.
*   **Failure Scenarios:** Target process not found; application fails to launch.
*   **Recovery Behavior:** Report specific OS error codes back to server.

### 4.7 Diagnostics
*   **Client Expectations:** Periodic health reporting.
*   **Server Responsibilities:** Capture and store CPU/RAM/OS telemetry for each client.
*   **Required APIs:** `TelemetrySvc`.
*   **Required Message Types:** `GET_DIAGNOSTICS` (Request), `EXECUTION_RESULT` (Payload).
*   **Required Data Models:** `DiagnosticsReport { CPU, RAM, Uptime, OS, NetworkStatus }`.
*   **Validation Rules:** Ignore negative usage values or impossible uptimes.
*   **Failure Scenarios:** Diagnostic provider on client fails.
*   **Recovery Behavior:** Use last known values; mark diagnostics as "Stale".

### 4.8 Logging
*   **Client Expectations:** Local daily rolling logs.
*   **Server Responsibilities:** Ingest critical error events from clients for centralized monitoring.
*   **Required APIs:** `CentralLogger`.
*   **Required Message Types:** `EVENT: ERROR_OCCURRED`.
*   **Required Data Models:** `LogEntry { PcId, Severity, Message, Exception }`.
*   **Validation Rules:** Implement rate-limiting to prevent log-flooding.
*   **Failure Scenarios:** Client generates high volume of errors.
*   **Recovery Behavior:** Alert admin of high error rate.

### 4.9 Watchdog & Recovery
*   **Client Expectations:** State re-enforcement after startup or crash.
*   **Server Responsibilities:** Detect `CLIENT_CONNECTED` event; verify client state against server DB.
*   **Required APIs:** `RecoveryManager`.
*   **Required Message Types:** `EVENT: CLIENT_CONNECTED`.
*   **Required Data Models:** `SyncState { CurrentSessionId, Status, KioskLocked }`.
*   **Validation Rules:** Server state always overrides client state.
*   **Failure Scenarios:** Client reports state that conflicts with server (e.g., Client thinks session is ENDED, Server says ACTIVE).
*   **Recovery Behavior:** Send immediate corrective commands to align client with server.

### 4.10 Configuration
*   **Client Expectations:** Use local JSON config; receive server updates.
*   **Server Responsibilities:** Centralize client settings (Heartbeat interval, Reconnect timers).
*   **Required APIs:** `ConfigDistributor`.
*   **Required Message Types:** `UPDATE_CONFIG`.
*   **Required Data Models:** `NetworkConfig { HeartbeatSeconds, ReconnectSeconds }`.
*   **Validation Rules:** Values must be within safety bounds (e.g., Heartbeat >= 5s).
*   **Failure Scenarios:** Config update fails to apply.
*   **Recovery Behavior:** Revert client to last known working config.

### 4.11 Heartbeat
*   **Client Expectations:** Regular liveness check-in.
*   **Server Responsibilities:** Update "Last Seen" timestamp; flag offline clients.
*   **Required APIs:** `LivenessSvc`.
*   **Required Message Types:** `HEARTBEAT`, `PONG`.
*   **Required Data Models:** `HealthStatus { PcId, IsOnline, LastSeen }`.
*   **Validation Rules:** Mark offline after 3 consecutive missed intervals.
*   **Failure Scenarios:** Intermittent LAN failure.
*   **Recovery Behavior:** Auto-reconnect flow on client; status update on server.

### 4.12 Client Registration
*   **Client Expectations:** Persistent identity.
*   **Server Responsibilities:** Map unique hardware IDs/MAC to internal `PcId`.
*   **Required APIs:** `RegistrationSvc`.
*   **Required Message Types:** Handled during `AUTH_RESPONSE`.
*   **Required Data Models:** `ClientRegistry { PcId, MAC, Hostname, AssignedIP }`.
*   **Validation Rules:** MAC address must be unique across the LAN.
*   **Failure Scenarios:** Duplicate hardware detected.
*   **Recovery Behavior:** Require manual admin intervention for registration conflicts.

### 4.13 Version Management
*   **Client Expectations:** Poll for updates; download verified binaries.
*   **Server Responsibilities:** Host update manifest; provide authenticated HTTP download service.
*   **Required APIs:** `UpdateAPI` (HTTP).
*   **Required Message Types:** HTTP GET / JSON manifest.
*   **Required Data Models:** `UpdateManifest { Version, Url, ChecksumSHA256 }`.
*   **Validation Rules:** Verify SHA256 before signaling update ready to client.
*   **Failure Scenarios:** Corrupt download; version mismatch.
*   **Recovery Behavior:** Retry download; if failing, stay on current version.

### 4.14 Error Handling
*   **Client Expectations:** Return structured error results.
*   **Server Responsibilities:** Capture all `EXECUTION_RESULT` with status "ERROR"; notify admin.
*   **Required APIs:** `AuditService`.
*   **Required Message Types:** `EXECUTION_RESULT`.
*   **Required Data Models:** `ErrorAudit { Action, ErrorMsg, Timestamp }`.
*   **Validation Rules:** Log all fields for troubleshooting.
*   **Failure Scenarios:** Server fails to process error report.
*   **Recovery Behavior:** Persist locally on server error log for manual review.

## 5. Required Server Subsystems

### 5.1 Server Core (TCP Engine)
*   **Purpose:** Low-level network management.
*   **Internal Components:** `ConnectionManager`, `PacketBuffer`, `KeepAliveService`.

### 5.2 Security Service
*   **Purpose:** Cryptographic enforcement.
*   **Internal Components:** `AuthHandler`, `EncryptionManager`, `SignatureVerifier`.

### 5.3 Internal Server Modules
*   **Session Engine:** Logic for timing and state transitions.
*   **Command Orchestrator:** Routing and tracking of administrative commands.
*   **Client Registry:** Directory and metadata of all PC terminals.
*   **Telemetry Hub:** Aggregator for diagnostic and performance data.

### 5.4 Persistence Layer
*   **Purpose:** Long-term storage.
*   **Responsibilities:** Storing client metadata, session history, logs, and command audits.

## 6. Architecture Overview

```text
    [ Admin Panel API ] (Future)
               |
               ▼
+---------------------------------------+
|           SAYRA SERVER CORE           |
|                                       |
|  +---------------------------------+  |
|  |       Command Orchestrator      |  |
|  +---------------------------------+  |
|  |  Session Engine | Client Reg.   |  |
|  +---------------------------------+  |
|  |  Security Svc   | Telemetry Hub |  |
|  +---------------------------------+  |
|  |           TCP Engine            |  |
+---------------------------------------+
               |
               ▼ (Secure TCP: AES/HMAC)
+---------------------------------------+
|            SAYRA CLIENT(S)            |
+---------------------------------------+
               |
               ▼
+---------------------------------------+
|           PERSISTENCE LAYER           |
| (Client Data, Sessions, Telemetry)    |
+---------------------------------------+
```

## 7. State & Persistence Requirements
The server must persist the following data categories:
1.  **Identity Data:** PC identifiers, MAC addresses, and assigned names.
2.  **Session Data:** Complete history of all user sessions and their final status.
3.  **Audit Logs:** Records of all commands issued and their execution results.
4.  **Diagnostics Data:** Historical resource usage for reporting and health analysis.
5.  **App Library:** Global list of application paths and configurations.

## 8. Gap Analysis

### 8.1 Mock Server Limitations
*   **Statelessness:** `mock_server.py` does not persist anything; all state is lost on restart.
*   **Identity Blindness:** Does not differentiate between clients; no PC registration.
*   **Update hosting:** No implementation for HTTP update manifest or file serving.
*   **Event handling:** Ignores state-sync and session-end events from clients.

### 8.2 Required Server Features (Production)
*   **SQL Persistence:** Mandatory for session and client tracking.
*   **Identity Mapping:** Ability to target commands to specific `PcId`.
*   **Security Vault:** Secure management of the `MasterKey`.
*   **HTTP Update Server:** Required for automated client maintenance.

### 8.3 Optional & Future Features
*   **Optional:** Centralized client log ingestion (can rely on local logs initially).
*   **Postponed:** Real-time screen capture / remote desktop (Phase 3+).
*   **Postponed:** Dynamic config push (Phase 2).

## 9. Recommended Development Roadmap
1.  **Phase 1: Secure Core:** Implement TCP Engine, Auth handshake, and Secure Transport.
2.  **Phase 2: Persistence:** Implement Client Registry and Session state storage.
3.  **Phase 3: Command & Control:** Build the Orchestrator for Run/Kill and Diagnostics.
4.  **Phase 4: Update Service:** Implement the HTTP binary distribution and checksum logic.
5.  **Phase 5: State Sync:** Implement the full `CLIENT_CONNECTED` recovery logic.
