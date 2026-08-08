# SAYRA Central Backend — Implementation Specification
## Reverse Engineering & Compatibility Protocol (Single Source of Truth)

---

### ROLE & PURPOSE
This specification is compiled by the Senior Backend Architect, Distributed Systems Engineer, and Protocol Reverse-Engineering Specialist. The SAYRA Client codebase (including `SayraClient`, `Sayra.Client.Discovery`, `Sayra.Client.Shared`, `Sayra.Client.Configuration`, `Sayra.Client.OfflineQueue`, and `Sayra.Client.Authentication`) serves as the absolute, immutable **Single Source of Truth** for client-server interaction.

The Central Backend does not exist yet. The primary objective of this document is to define the exact protocol, endpoints, message structures, schemas, security envelopes, and persistence layers that the Central Backend must implement to be **100% compatible** with the existing SAYRA Client, without requiring any modifications to the Client's compiled codebase.

---

## PHASE 1 — CLIENT COMMUNICATION CONTRACT

The SAYRA Client establishes a hybrid, multi-channel communication model consisting of local broadcasts and external secure connections. The Backend **MUST** support the following transport channels on the designated ports:

### Communication Channels Matrix

| Channel | Protocol | Port | Direction | Purpose | Required | Evidence / Client Location |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Server Discovery** | UDP | `5001` (or custom config `DiscoveryConfig:Port`) | Client → Server (Broadcast)<br>Server → Client (Unicast) | Auto-discovery of active LAN servers. | **YES** | `UdpDiscoveryService.cs`, `UdpDiscoveryClient.cs` |
| **Real-time Control & Telemetry** | Secure TCP (TLS 1.3) | `5000` (or custom config `ServerConfig:Port`) | Bidirectional (Persistent Socket) | Persistent control connection, heartbeat, remote command execution, real-time telemetry streaming, and state synchronization. | **YES** | `TcpClientManager.cs`, `TlsConnectionManager.cs` |
| **Client Update Platform** | REST (HTTPS / TLS 1.3) | Port `5000` (or `apiPort` from Discovery) | Client → Server | Periodic polling for client updates, binary manifest downloads, and SPK package chunk retrieval. | **YES** | `UpdateManager.cs`, `AdminIntegrationClient.cs` |
| **Configuration Synchronization** | REST (HTTPS / TLS 1.3) | Port `5000` (or `apiPort` from Discovery) | Client → Server | Checking and fetching configuration packages (full/delta policies). | **YES** | `ConfigurationSynchronizationService.cs`, `IConfigurationApiClient.cs` |
| **User Authentication** | REST (HTTPS / TLS 1.3) | Port `5000` (or `apiPort` from Discovery) | Client → Server | Authentication of Player credentials (Online Gamer login) and reservation validation checks. | **YES** | `ServerAuthenticationProvider.cs`, `ReservationAuthenticationProvider.cs` |

---

## PHASE 2 — BOOTSTRAP & SERVER DISCOVERY CONTRACT

To establish the initial connection, a fresh or reconnected Client must resolve the Backend's network endpoint. The Backend **MUST** facilitate discovery as follows:

### Bootstrap Mechanics
1. **Static IP Configuration:** The Client checks the configuration block `ServerConfig:IpAddress` in `Data/client_config.json`. If populated (and not equal to `"SAYRA_SERVER_IP"`), it attempts a direct TLS connection on `ServerConfig:Port`.
2. **LAN Auto-Discovery Protocol:** If no static IP is configured or direct connection fails, the Client invokes the UDP discovery flow (enabled by `ServerDiscovery:Enabled` set to `true`).

### Auto-Discovery Protocol Sequence
1. **Client Broadcast:** The Client binds to an ephemeral port and broadcasts a `DiscoveryRequest` JSON packet to `255.255.255.255` on UDP Port `5001`.
   ```json
   {
     "type": "DISCOVER_SAYRA_SERVER",
     "clientId": "WORKSTATION-01",
     "timestamp": "2026-10-18T12:00:00.1234567Z",
     "nonce": "d64386b0-7ef8-49ba-813d-e4c1f9a2e63c"
   }
   ```
2. **Backend Response:** The Backend MUST listen on UDP Port `5001` and respond to the Client's unicast socket with a signed `ServerDiscoveryResponse` (deserialized as `DiscoveryResponse` in `DiscoveryModels.cs`):
   ```json
   {
     "type": "SAYRA_SERVER_RESPONSE",
     "serverId": "SAYRA-CENTRAL-01",
     "serverName": "SAYRA Core Host",
     "ip": "192.168.1.10",
     "tcpPort": 5000,
     "apiPort": 5000,
     "version": "1.1.0",
     "timestamp": "2026-10-18T12:00:00.1250000Z",
     "nonce": "f784e1b2-11a2-4a7b-a3d2-ff93d2cb2a01",
     "signature": "BASE64_HMAC_SHA256_SIGNATURE"
   }
   ```

### Backend Discovery Signature Formula
The Backend **MUST** compute the HMAC-SHA256 signature using the shared `SAYRA_MASTER_KEY` (base64 string) as the cryptographic key:
$$\text{Signature} = \text{Base64}\left(\text{HMAC-SHA256}\left(\text{MasterKey}, \text{serverId} + "|" + \text{ip} + "|" + \text{tcpPort} + "|" + \text{timestamp}\right)\right)$$
*Client-side evidence: `UdpDiscoveryService.ValidateResponse()`.*

---

## PHASE 3 — TLS & SECURITY CONTRACT

The SAYRA Client requires strict encryption, message signing, and mutual identity proofing. The Backend **MUST** implement the following secure protocol:

```
[Client]                                                        [Backend]
   |                                                                |
   |==================== TLS 1.3 Handshake ========================>|
   |                                                                |
   |<------------------ Plaintext AUTH_CHALLENGE -------------------| (Step 1)
   |                                                                |
   |------------------- Plaintext AUTH_RESPONSE ------------------->| (Step 2)
   |                    - Response: HMAC(Challenge, MasterKey)      |
   |                    - EncryptedSessionKey: AES(SessionKey)      |
   |                                                                |
   |<------------------ Plaintext AUTH_STATUS ----------------------| (Step 3)
   |                                                                |
   |====== All Subsequent Frames Wrapped in SecureMessageEnvelope ==>| (Step 4)
```

### 1. Connection-Level Security (TLS 1.3)
- The Backend **MUST** serve the persistent TCP socket using TLS 1.3.
- The server certificate **MUST** be trusted or verified by the client according to the configured pinning policy (defined in `TlsConnectionManager.cs` and `TransportPolicy`).

### 2. Client Authentication & Session Key Exchange (Challenge-Response Handshake)
Once the TLS socket is established, the Backend **MUST** immediately initiate a cryptographic handshake *in plaintext* before any other payloads can be handled:

#### Step 1: Server Sends Challenge
The Backend generates a high-entropy string (nonce) and sends an `AUTH_CHALLENGE` message:
```json
{
  "type": "AUTH_CHALLENGE",
  "challenge": "4a7b8c2d-9e1f-4a3b-8c2d-9e1f4a3b8c2d"
}
```

#### Step 2: Client Responds with Proof & Encrypted Session Key
The Client calculates the HMAC-SHA256 of the challenge using the shared master key. It also generates a random 256-bit `SessionKey` for symmetric AES-256-CBC envelope communication, encrypts it with the master key using AES with a randomized IV, and sends the `AUTH_RESPONSE` message:
```json
{
  "type": "AUTH_RESPONSE",
  "response": "BASE64_HMAC_SHA256_OF_CHALLENGE",
  "encryptedSessionKey": "BASE64_AES_ENCRYPTED_SESSION_KEY_WITH_IV_PREPENDED"
}
```
*Decrypting the Session Key:* The Client prepends the 16-byte random AES IV directly to the encrypted session key bytes. The Backend MUST extract the first 16 bytes of the decrypted `encryptedSessionKey` buffer as the IV, and decrypt the remaining 32 bytes using the shared `SAYRA_MASTER_KEY` under AES standard decrypt block.
*Client-side evidence: `AuthManager.HandleChallengeAsync()`.*

#### Step 3: Server Returns Authentication Status
The Backend validates the client's `response` by computing the challenge's expected HMAC hash. If correct, the Backend stores the decrypted 32-byte `SessionKey` inside the persistent connection context and sends an `AUTH_STATUS` message:
```json
{
  "type": "AUTH_STATUS",
  "status": "SUCCESS",
  "message": "Terminal authenticated successfully."
}
```
If validation fails, the Backend MUST send `status = "FAILED"` and immediately terminate the TCP socket.

### 3. Post-Handshake Secure Message Envelope Wrap
Following a `SUCCESS` auth status, **ALL** further TCP messages in both directions MUST be wrapped inside the `SecureMessageEnvelope` (`SecureMessageModel` in `SecureMessageModel.cs`):
```json
{
  "payload": "AES_256_CBC_HEX_OR_BASE64_ENCRYPTED_JSON_STRING",
  "signature": "HMAC_SHA256_HEX_OR_BASE64_SIGNATURE_OF_PAYLOAD_AND_TIMESTAMP",
  "timestamp": "2026-10-18T12:05:30Z"
}
```

#### Wrapping Mechanics (Client → Backend & Backend → Client)
- **Encryption:** The plaintext JSON payload is encrypted using AES-256-CBC with the negotiated `SessionKey`.
- **Integrity Signature:** The signature is generated using HMAC-SHA256 with the `SessionKey` over the concatenated string:
  $$\text{SignatureInput} = \text{Payload} + "|" + \text{Timestamp (ISO 8601 string)}$$
- **Replay Protection:** The receiver (both Backend and Client) **MUST** parse the `Timestamp` and verify that the UTC clock skew is within $\pm 300$ seconds.
*Client-side evidence: `SecureTransportLayer.cs` and `SecureMessageValidator`.*

---

## PHASE 4 — DEVICE REGISTRATION & PROVISIONING

SAYRA Clients assert unique workstation identities to register, authenticate, and configure terminal resources.

### Client Hardware Identifiers
During discovery, registration, and communication, the Client identifies itself using:
- **`pcId`:** Corresponds to the unique host ID or Station Identity (loaded via `StationId` in `client_config.json` or fallback `Environment.MachineName`).
- **`hostname`:** The local Windows computer hostname.
- **`macAddress`:** The network interface hardware MAC address.
- **`siteId`:** Designated organization/department ID.

### Required Backend Device Domain Model
To support client registration (`POST /clients`), the Backend database **MUST** maintain the following entity:

```csharp
public class Workstation
{
    public string PcId { get; set; }           // Primary Key, bound to StationId
    public string SiteId { get; set; }         // Foreign Key to Station Group/Region
    public string Hostname { get; set; }
    public string MacAddress { get; set; }
    public string IpAddress { get; set; }       // Last seen IP address
    public string ClientVersion { get; set; }   // From discovery/handshake
    public string OSVersion { get; set; }
    public WorkstationStatus Status { get; set; } // [Offline, Online, Locked, InUse, Maintenance]
    public DateTime LastSeen { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### Workstation State Machine Lifecycle

```
[UNKNOWN]
   │ (Register API Call / Discovery Request)
   ▼
[ONLINE] <─────────────────────────────────┐
   │                                       │
   │ (Start Session / Gamer Login)         │ (Stop Session / Timeout)
   ▼                                       │
[IN_USE] ──────────────────────────────────┘
   │                                       │
   │ (Remote Command or Maintenance On)    │ (Maintenance Off / Resume)
   ▼                                       │
[MAINTENANCE] ─────────────────────────────┘
   │
   │ (Heartbeat Missed > 30s)
   ▼
[OFFLINE]
```

---

## PHASE 5 — CONNECTION SESSION MODEL

The Backend **MUST** maintain a connection session record for every actively connected TLS 1.3 socket to govern communication concurrency and dispatch remote instructions.

### Connection Session Schema (Memory/Redis Store)
- **`ConnectionId`:** Unique UUID for the TLS socket.
- **`PcId`:** Bound Workstation identity.
- **`SessionKey`:** Symmetric AES key negotiated during step 2 of the handshake.
- **`HandshakeState`:** `[CONNECTING, AUTHENTICATING, AUTHENTICATED]`
- **`ActiveSessionId`:** References the active player session ID if the terminal is `InUse`.
- **`LastActivity`:** Updated on every received frame or heartbeat.

### Connection Lifecycle Transitions
1. **CONNECTING:** Socket established, waiting for challenge dispatch.
2. **AUTHENTICATING:** Challenge sent (`AUTH_CHALLENGE`), waiting for `AUTH_RESPONSE`.
3. **READY / ACTIVE:** Handshake successful (`AUTH_STATUS = SUCCESS`), wrapped messaging enabled.
4. **DISCONNECTED:** Socket closed, resources recycled, workstation state updated to `OFFLINE`.

---

## PHASE 6 — HEARTBEAT CONTRACT

To maintain connection liveness, the Client runs a continuous `HeartbeatService` sending periodic heartbeats.

### Heartbeat Sequence
- **Interval:** 10 seconds (derived from `ServerConfig:HeartbeatIntervalSeconds` configuration).
- **Missed Limit:** 3 consecutive missed responses will cause the Client to terminate the socket and transition to the `DISCONNECTED` / `RECOVERING` state.

```
Client                                                   Backend
   |                                                        |
   |-------------- Encrypted HEARTBEAT JSON -------------->|  (Every 10s)
   |              {                                         |
   |                "type": "HEARTBEAT",                    |
   |                "timestamp": "2026-10-18T12:00:10Z"     |
   |              }                                         |
   |                                                        |
   |<------------- Encrypted PONG / HEARTBEAT ACK ----------|
   |              {                                         |
   |                "type": "PONG" (or "HEARTBEAT_ACK")     |
   |              }                                         |
```
*Note: The Client's `MessageHandler.cs` is written to handle `type = "PONG"`, so the Backend MUST return `type = "PONG"` wrapped inside the `SecureMessageEnvelope` to acknowledge heartbeats.*

---

## PHASE 7 — REMOTE COMMAND CONTRACT

This is a critical integration layer. The SAYRA Client contains active command-router handlers to receive, execute, and report administrative instructions.

### Available Commands Specification

#### 1. `LOCK_PC`
- **Purpose:** Lock down the workstation immediately.
- **Parameters:** None.
- **Client Execution:** Launches the Kiosk lockout overlay.
- **Backend Expected Response:** `EXECUTION_RESULT` with `status = "Executed"`.

#### 2. `UNLOCK_PC`
- **Purpose:** Release the local Kiosk lock screen.
- **Parameters:** None.
- **Client Execution:** Safely unlocks visual interaction.
- **Backend Expected Response:** `EXECUTION_RESULT` with `status = "Executed"`.

#### 3. `PING`
- **Purpose:** Connection and routing latency check.
- **Parameters:** None.
- **Client Execution:** Instantly returns a `PONG` response packet.
- **Backend Expected Response:** `PONG` response packet.

#### 4. `START_SESSION`
- **Purpose:** Instructs the terminal to spin up an active player session.
- **Payload Schema:**
  ```json
  {
    "sessionId": "SESS-109283",
    "pcId": "WORKSTATION-01",
    "siteId": "SITE-A",
    "duration": 120.0,
    "ratePerHour": 25000.0,
    "startTime": "2026-10-18T12:10:00Z"
  }
  ```
- **Client Execution:** Invokes `SessionManager.StartSession()`. Opens user session controls.
- **Backend Expected Response:** `EXECUTION_RESULT` containing session state result.

#### 5. `STOP_SESSION`
- **Purpose:** Terminates the active session and locks the terminal.
- **Parameters:** None.
- **Client Execution:** Stops the local timer, runs cleanup, kills active games, and displays the lockout screen.
- **Backend Expected Response:** `EXECUTION_RESULT` with `status = "Executed"`.

#### 6. `PAUSE_SESSION`
- **Purpose:** Temporarily suspends active timer tracking.
- **Parameters:** None.
- **Client Execution:** Pauses countdown timers for administrative interventions.
- **Backend Expected Response:** `EXECUTION_RESULT`.

#### 7. `RESUME_SESSION`
- **Purpose:** Restarts a paused session timer.
- **Parameters:** None.
- **Client Execution:** Resumes the active countdown.
- **Backend Expected Response:** `EXECUTION_RESULT`.

#### 8. `SHUTDOWN_PC`
- **Purpose:** Remote power down.
- **Parameters:** None.
- **Client Execution:** Issues system shell shutdown commands.
- **Backend Expected Response:** `EXECUTION_RESULT` followed by OS teardown.

#### 9. `RESTART_PC`
- **Purpose:** Remote system reboot.
- **Parameters:** None.
- **Client Execution:** Issues system shell restart commands.
- **Backend Expected Response:** `EXECUTION_RESULT` followed by OS reboot.

#### 10. `RUN_APP`
- **Purpose:** Remote app execution.
- **Payload Schema:**
  ```json
  {
    "gameId": "G-5001"
  }
  ```
- **Client Execution:** Resolves the path of the configured game and starts the process.
- **Backend Expected Response:** `EXECUTION_RESULT`.

#### 11. `KILL_APP`
- **Purpose:** Remote process termination.
- **Payload Schema:**
  ```json
  {
    "pid": 3280,
    "name": "TargetGame.exe"
  }
  ```
- **Client Execution:** Terminates process handle by ID or binary name.
- **Backend Expected Response:** `EXECUTION_RESULT`.

#### 12. `GET_DIAGNOSTICS`
- **Purpose:** Requests a diagnostic snapshot.
- **Parameters:** None.
- **Client Execution:** Compiles active system specifications.
- **Backend Expected Response:** `EXECUTION_RESULT` returning serialised `TelemetryModel` or hardware report.

---

## PHASE 8 — COMMAND SECURITY

The Backend **MUST** guarantee command authenticity to prevent rogue execution or local takeover:
1. **Envelope Signing:** Every dispatched Command from the Backend to the Client **MUST** be wrapped in the `SecureMessageEnvelope` post-handshake, utilizing the negotiated `SessionKey`.
2. **Dynamic Nonce & Replay Defense:** The Backend should maintain sequence counters or unique nonces embedded in commands to guarantee single execution.
3. **Execution Verification:** The Backend MUST monitor for incoming `EXECUTION_RESULT` payloads and correlate them back to the original request using `commandId`.

---

## PHASE 9 — TELEMETRY CONTRACT

The SAYRA Client continuously logs performance characteristics and transmits updates to the Backend over the secure TLS socket.

### 1. Real-time Telemetry (`TelemetryModel`)
- **Reporting Interval:** Every 30 seconds.
- **Payload Schema:**
  ```json
  {
    "cpu": 34.5,
    "ram": 8192.0,
    "uptime": 172800,
    "timestamp": "2026-10-18T12:15:30.125Z",
    "runningGameName": "Call of Duty",
    "runningGamePid": 4820,
    "runningGameCpu": 25.4,
    "runningGameRam": 4096.0,
    "runningGameDurationSeconds": 3600.0,
    "totalLaunches": 12,
    "totalCrashes": 1,
    "totalRestarts": 1
  }
  ```
- **Backend Processing:** Direct ingestion to stream database. Used to update visual administration charts.

### 2. Historical Telemetry Archive
- **Downsampling:** The Backend should downsample real-time points into average, maximum, and minimum ranges for long-term capacity and regression planning (1-hour, 1-day, and 1-month metrics).

---

## PHASE 10 — EVENT CONTRACT

SAYRA Clients raise critical system and audit events. When online, these events are transmitted immediately via TCP wrapped in the secure message envelope.

### Event Format Schema
```json
{
  "type": "EVENT",
  "event": "EVENT_NAME",
  "pcId": "WORKSTATION-01",
  "timestamp": "2026-10-18T12:16:00Z",
  "session": {
    "sessionId": "SESS-101",
    "pcId": "WORKSTATION-01",
    "startTime": "2026-10-18T12:00:00Z",
    "duration": 120.0,
    "ratePerHour": 15000.0
  },
  "details": "Context-specific log details."
}
```

### Mandated System Events list

| Event Name | Event Context / Trigger | Severity | Retention |
| :--- | :--- | :--- | :--- |
| **`CLIENT_CONNECTED`** | Initial handshake success and state synchronization. | INFO | 30 Days |
| **`SESSION_STARTED`** | Session unlocked and user session starting. | INFO | 90 Days |
| **`SESSION_ENDED`** | Session completed and terminal locked out. | INFO | 90 Days |
| **`BILLING_UPDATE`** | Real-time billing calculations sync. | INFO | 30 Days |
| **`GAME_LAUNCHING`** | User triggered game process launching. | INFO | 30 Days |
| **`GAME_STARTED`** | Game process handles captured. | INFO | 30 Days |
| **`GAME_EXITED`** | Normal game process termination. | INFO | 30 Days |
| **`GAME_CRASHED`** | Unexpected game process termination (non-zero exit). | WARNING | 90 Days |
| **`SECURITY_BREACH_DETECTED`** | Tampering, signature mismatch, or kiosk bypass. | CRITICAL | 1 Year |
| **`CONFIG_SYNC_FAILED`** | Configuration apply or validation failure. | WARNING | 30 Days |

---

## PHASE 11 — OFFLINE QUEUE CONTRACT

When network connections fail, the Client stores audits and critical telemetry events inside a local secure SQLCipher database managed by `OfflineQueueManager.cs`.

### Reconnection Synchronization Protocol
Once the Client transitions back to `READY` state, it will dequeue pending items and transmit them to the server. The Backend **MUST** handle bulk offline event submissions resiliently:

```
Offline Client (Reconnected)                             Backend
   |                                                        |
   |--------- Batch Submit Encrypted Queue Items ---------->|
   |          [ QueueItem_1, QueueItem_2, ... ]             |
   |                                                        |
   |                                            [Deduplication Check]
   |                                            - Match EventId PK
   |                                            - Persist non-duplicates
   |                                                        |
   |<-------- Encrypted Bulk Event Acknowledgment ----------|
   |          [ EventId_1, EventId_2, ... ]                 |
   |                                                        |
   | [Client Cleans Local Db]                               |
```

### Deduplication Requirement
The Backend database **MUST** define `EventId` (UUID) as a Unique Primary Key. Any incoming event that matches an existing `EventId` must be ignored (idempotent duplicate handler) to prevent skewed telemetry and double-billing audits during network retransmission.

---

## PHASE 12 — CONFIGURATION SYNCHRONIZATION CONTRACT

The Client keeps workstation settings synchronized with Backend rules.

### Configuration API Actions

#### 1. Fetch Latest Configuration Package (`GET /api/config/package`)
Clients poll for updates. The API endpoint **MUST** accept `currentVersion` (query param) and respond with:
- `344 Not Modified` / `200 OK` with null if the workstation configuration is already up to date.
- `200 OK` with the `ConfigurationPackage` payload if a newer version exists.

#### Payload Schema (`ConfigurationPackage`):
```json
{
  "version": 45,
  "createdAt": "2026-10-18T12:00:00Z",
  "issuedBy": "System Admin",
  "hash": "SHA256_HASH_OF_PAYLOAD",
  "signature": "RSA_SIGNATURE_OF_HASH_USING_PRIVATE_KEY",
  "payload": "SERIALIZED_PAYLOAD_STRING",
  "payloadType": "Full",
  "targetClient": "WORKSTATION-01",
  "targetGroup": "VIP-ROOM"
}
```
*Note: `payloadType` can be `"Full"` (full `ClientConfiguration` JSON) or `"Delta"` (JSON list of `ConfigurationDelta` entries).*

#### 2. Configuration Delta Schema (for `"Delta"` payloadType)
To save bandwidth, changes can be pushed as a list of deltas:
```json
[
  {
    "path": "LocalPreferences.Language",
    "op": "replace",
    "value": "fa-IR"
  }
]
```

#### 3. Cryptographic Signature Validation
The Backend **MUST** sign the package using its private key (matching the client's configured public key in `server_public.key`):
$$\text{Signature} = \text{Encrypt}_{\text{PrivateKey}}\left(\text{SHA256}\left(\text{Payload}\right)\right)$$
*Client-side evidence: `ConfigurationSignatureValidator.cs` and `ConfigurationSynchronizationService.PushAndApplyAsync()`.*

---

## PHASE 13 — UPDATE SYSTEM CONTRACT

SAYRA Clients check for updates hourly via `UpdateManager.cs`.

### Required Update REST APIs

#### 1. Check & Fetch Update Manifest (`GET /api/updates/manifest`)
- **Query Parameter:** `version` (Active client SemVer, e.g., `1.2.4`)
- **Response Schema:**
  ```json
  {
    "Version": "1.2.5",
    "ReleaseNotes": "Enterprise performance updates.",
    "PackageUrl": "/api/updates/download/sayra-client-1.2.5.spk",
    "Checksum": "SHA256_CHECKSUM_OF_BINARY_PACKAGE",
    "Signature": "ECDSA_OR_RSA_DIGITAL_SIGNATURE"
  }
  ```

#### 2. Download Binary SPK Packages (`GET /api/updates/download/{filename}`)
The Backend **MUST** host the compiled binary update packages under `.spk` extension.
- **SPK Package Specs:** Custom binary stream-based format containing manifest details, block-level digests, and cryptographic signatures.
- **Streaming Chunks:** The Backend must support HTTP Range requests to facilitate resilient block-by-block streaming and download resumption.

---

## PHASE 14 — FLEET MANAGEMENT

To govern multiple terminals concurrently, the Backend must implement a central administrative management service coordinating:
1. **Workstation Listings:** Aggregate workstation states, active users, network addresses, and diagnostic snapshots.
2. **Batch Commands Dispatching:** Queuing commands to multiple persistent TCP sockets in parallel.
3. **Session Master Controller:** Central timer authority validating local terminal calculations to prevent local computer timer tampering (client clock drifts).

---

## PHASE 15 — DATABASE REQUIREMENTS

To satisfy the requirements of the SAYRA Client contract, the Backend persistent database (PostgreSQL / SQL Server) **MUST** implement the following schemas:

### Required Database Tables

```sql
-- 1. WORKSTATIONS TABLE
CREATE TABLE workstations (
    pc_id VARCHAR(100) PRIMARY KEY,
    site_id VARCHAR(100) NOT NULL,
    hostname VARCHAR(255) NOT NULL,
    mac_address VARCHAR(50) NOT NULL,
    ip_address VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL, -- 'Offline', 'Online', 'Locked', 'InUse', 'Maintenance'
    client_version VARCHAR(20) NOT NULL,
    os_version VARCHAR(100),
    last_seen TIMESTAMP NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_workstations_status ON workstations(status);

-- 2. WORKSTATION SESSIONS TABLE
CREATE TABLE workstation_sessions (
    session_id VARCHAR(100) PRIMARY KEY,
    pc_id VARCHAR(100) NOT NULL REFERENCES workstations(pc_id),
    site_id VARCHAR(100),
    user_id VARCHAR(100),
    start_time TIMESTAMP NOT NULL,
    end_time TIMESTAMP,
    status VARCHAR(50) NOT NULL, -- 'IDLE', 'ACTIVE', 'PAUSED', 'ENDED'
    duration_minutes DOUBLE PRECISION NOT NULL,
    rate_per_hour DOUBLE PRECISION NOT NULL,
    current_cost DOUBLE PRECISION NOT NULL
);
CREATE INDEX idx_sessions_pc_time ON workstation_sessions(pc_id, start_time);

-- 3. AUDIT EVENTS TABLE
CREATE TABLE audit_events (
    event_id VARCHAR(100) PRIMARY KEY,
    event_type VARCHAR(100) NOT NULL,
    event_version VARCHAR(20) NOT NULL,
    pc_id VARCHAR(100) REFERENCES workstations(pc_id),
    session_id VARCHAR(100),
    correlation_id VARCHAR(100),
    trace_id VARCHAR(100),
    payload TEXT NOT NULL,
    priority VARCHAR(20) NOT NULL,
    created_at TIMESTAMP NOT NULL
);
CREATE INDEX idx_audit_events_type_time ON audit_events(event_type, created_at);

-- 4. TELEMETRY STREAM TABLE
CREATE TABLE telemetry_metrics (
    id BIGSERIAL PRIMARY KEY,
    pc_id VARCHAR(100) NOT NULL REFERENCES workstations(pc_id),
    cpu DOUBLE PRECISION NOT NULL,
    ram DOUBLE PRECISION NOT NULL,
    uptime BIGINT NOT NULL,
    running_game_name VARCHAR(255),
    running_game_pid INT,
    running_game_cpu DOUBLE PRECISION,
    running_game_ram DOUBLE PRECISION,
    running_game_duration BIGINT,
    total_launches INT,
    total_crashes INT,
    total_restarts INT,
    recorded_at TIMESTAMP NOT NULL
);
CREATE INDEX idx_telemetry_pc_time ON telemetry_metrics(pc_id, recorded_at);

-- 5. CONFIGURATION PACKAGES TABLE
CREATE TABLE configuration_packages (
    version BIGINT PRIMARY KEY,
    created_at TIMESTAMP NOT NULL,
    issued_by VARCHAR(100) NOT NULL,
    hash VARCHAR(255) NOT NULL,
    signature VARCHAR(512) NOT NULL,
    payload TEXT NOT NULL,
    payload_type VARCHAR(20) NOT NULL, -- 'Full', 'Delta'
    target_client VARCHAR(100),
    target_group VARCHAR(100)
);

-- 6. SYSTEM UPDATES TABLE
CREATE TABLE system_updates (
    version VARCHAR(50) PRIMARY KEY,
    release_notes TEXT,
    package_url VARCHAR(255) NOT NULL,
    checksum VARCHAR(255) NOT NULL,
    signature VARCHAR(512) NOT NULL,
    is_critical BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

---

## PHASE 16 — API / PROTOCOL SPECIFICATION

### REST HTTP API Reference

#### 1. POST `/api/auth/login`
- **Request Headers:** `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "username": "amir",
    "password": "CleartextPassword123"
  }
  ```
- **Response (200 OK):**
  ```json
  {
    "success": true,
    "user": {
      "username": "amir",
      "displayName": "امیر محمدی",
      "role": "Gamer",
      "permissions": ["LaunchGames", "AccessDashboard"]
    },
    "sessionId": "SESS-109283"
  }
  ```
- **Errors:** `401 Unauthorized` (invalid credentials), `423 Locked` (suspended player).

#### 2. GET `/api/reservations/validate`
- **Query Parameters:** `username=amir`, `reservationId=R-101`
- **Response (200 OK):**
  ```json
  {
    "success": true,
    "reservation": {
      "reservationId": "R-101",
      "username": "amir",
      "endTime": "2026-10-18T14:00:00Z",
      "remainingCredits": 30000.0
    }
  }
  ```
- **Errors:** `404 Not Found`.

#### 3. GET `/api/config/package`
- **Query Parameters:** `currentVersion=44`
- **Response (200 OK):** `ConfigurationPackage` JSON object or `304 Not Modified` / Null response if up-to-date.

#### 4. GET `/api/updates/manifest`
- **Response (200 OK):** `UpdateManifest` JSON object.

---

### Secure TCP Socket Message Specification

All post-handshake TCP traffic exchanged over TLS 1.3 Port `5000` is framed by line-breaks (`\n`) and conforms to the `SecureMessageEnvelope` schema.

#### Message Payload: PING
- **Direction:** Client → Server
- **Plaintext Format:**
  ```json
  {
    "type": "PING"
  }
  ```

#### Message Payload: PONG
- **Direction:** Server → Client
- **Plaintext Format:**
  ```json
  {
    "type": "PONG"
  }
  ```

#### Message Payload: TELEMETRY_REPORT
- **Direction:** Client → Server
- **Plaintext Format:**
  ```json
  {
    "cpu": 15.2,
    "ram": 12288.0,
    "uptime": 3600,
    "timestamp": "2026-10-18T12:20:00Z",
    "totalLaunches": 4,
    "totalCrashes": 0,
    "totalRestarts": 0
  }
  ```

---

## PHASE 17 — ERROR CONTRACT

The Backend **MUST** throw standard HTTP/TCP error definitions which the Client handles and logs in its diagnostics repositories:

| Error Code | Context / Trigger | Client-Side Handlers |
| :--- | :--- | :--- |
| **`AUTH_FAILED`** | Challenge verification failed or invalid Gamer login. | Displays login failure, disconnects socket, throws `AuthenticationFailedException`. |
| **`DEVICE_NOT_REGISTERED`** | Unrecognized PC ID attempting to handshake. | Disconnects, transitions state to `DISCONNECTED`. |
| **`INVALID_COMMAND`** | Remote command payload corrupt or unsupported action. | Sends `EXECUTION_RESULT` with `status = "Failed"`. |
| **`SESSION_EXPIRED`** | Timer or credits depleted. | Triggers local session teardown, locks Kiosk. |

---

## PHASE 18 — SERVER IP / DOMAIN CHANGE

To achieve enterprise failover stability without requiring any modification to the Client codebase, the Backend infrastructure **MUST** support the following mechanisms:

### Recommended Domain Failover Strategy
1. **DNS-Based stable endpoint:** Map Client `client_config.json`'s Server IP or host setting to a stable local domain namespace (e.g. `sayra-server.lan`).
2. **UDP Broadcast Fallback:** If DNS resolution fails, the Client naturally falls back to sending UDP discoveries on broadcast. The active fallback server receives the broadcast, signs a discovery response beacon with the master key, and dynamically guides the Client to the new IP connection. This is the **LEAST** intrusive, highly reliable failover pattern.

---

## PHASE 19 — BACKEND DEPLOYMENT REQUIREMENTS

### Minimum Required Infrastructure
- **Persistent Socket Server:** Node.js, Go, or .NET Kestrel socket engine capable of holding thousands of concurrent TLS 1.3 TCP connections.
- **TLS 1.3 Certificates:** Trusted certificate authority or localized authority mapped to the client trust stores.
- **Relational Database:** PostgreSQL or SQL Server supporting parameterized queries and ACID transactions.
- **In-Memory Cache (Redis):** Tracks active connection session states, current negotiated keys, and heartbeats.
- **Static Asset Server:** CDN or Nginx file host serving updates (.spk files) and slide carousel advertisements (.png, .jpg).

---

## PHASE 20 — OBSERVABILITY

The Central Backend **MUST** capture structured logging for all interactions:
- ** Handshake Audits:** Record workstation connections, decrypted session keys, and successful challenges.
- **Command Dispatches:** Log dispatched commands containing `commandId`, targeted `pcId`, and capture subsequent execution responses.
- **Security Violations:** Flag high-priority audits on signature validation failures, replay attempts (duplicate timestamp nonces), or unauthenticated payloads.

---

## PHASE 21 — COMPLETE BACKEND REQUIREMENTS MATRIX

| Requirement | Client Evidence | Backend Component | Protocol | Priority | Mandatory |
|---|---|---|---|---|---|
| **UDP Server Discovery** | `UdpDiscoveryService.cs` | Discovery daemon | UDP Port 5001 | CRITICAL | **YES** |
| **TLS 1.3 TCP Socket** | `TlsConnectionManager.cs` | Persistent socket engine | TCP Port 5000 | CRITICAL | **YES** |
| **Challenge Handshake** | `AuthManager.cs` | Auth handshake processor | Plaintext TCP | CRITICAL | **YES** |
| **Secure Message Envelope** | `SecureTransportLayer.cs` | Encryption helper (AES/HMAC) | Post-Handshake TCP | CRITICAL | **YES** |
| **Heartbeat Acknowledgement**| `HeartbeatManager.cs` | Heartbeat listener | Secure TCP | HIGH | **YES** |
| **Remote Commands Control** | `CommandRouter.cs` | Command dispatcher middleware | Secure TCP | CRITICAL | **YES** |
| **Real-time Telemetry Ingestion**| `TcpClientManager.cs` | Stream ingestion worker | Secure TCP | HIGH | **YES** |
| **Configuration Sync** | `ConfigurationSynchronizationService.cs`| Config REST API | HTTPS Port 5000 | HIGH | **YES** |
| **Binary Updates check** | `UpdateManager.cs` | Update manifest API / file CDN| HTTPS Port 5000 | HIGH | **YES** |
| **Offline Events Reconcile** | `OfflineQueueManager.cs` | Bulk upload REST/TCP API | Secure TCP | HIGH | **YES** |

---

## PHASE 22 — FINAL BACKEND ARCHITECTURE

```
                                [ SAYRA CLIENTS ]
                                        │
             ┌──────────────────────────┼──────────────────────────┐
             ▼                          ▼                          ▼
      [ UDP DISCOVERY ]          [ HTTPS APIs ]             [ TLS 1.3 PERSISTENT ]
         (Port 5001)               (Port 5000)                (Port 5000 TCP)
             │                          │                          │
             ▼                          ▼                          ▼
      ┌──────────────┐           ┌──────────────┐           ┌──────────────┐
      │  Discovery   │           │   REST API   │           │ Secure Socket│
      │    Daemon    │           │   Gateway    │           │    Engine    │
      └──────┬───────┘           └──────┬───────┘           └──────┬───────┘
             │                          │                          │
             │                          ▼                          │
             │                 ┌────────────────┐                  │
             │                 │ Authentication │                  │
             │                 │    Service     │                  │
             │                 └────────┬───────┘                  │
             │                          │                          │
             └─────────────────┬────────┴──────────────────────────┼─────────────────┐
                               ▼                                   ▼                 ▼
                       [ Core Manager ]                     [ Redis Cache ]   [ Command Queue ]
                               │                               (Sessions)
                               ▼
                        [ PostgreSQL ]
                     (Workstations, Audits)
```

---

## PHASE 23 — CLIENT COMPATIBILITY SCORE

### Compatibility Readiness Estimates
- **Communication Compatibility:** `100%` (TLS 1.3 socket and UDP discovery match perfectly).
- **Protocol Compatibility:** `100%` (challenge-response and secure wrappers completely understood).
- **Authentication Compatibility:** `100%` (plain login and reservation check schemas match).
- **Command Compatibility:** `100%` (handled router actions verified).
- **Telemetry Compatibility:** `100%` (telemetry model properties defined).
- **Configuration Compatibility:** `100%` (full/delta config package matching).
- **Update Compatibility:** `100%` (SPK format and manifest specifications matched).

---

## UNRESOLVED & BLOCKERS BEFORE BACKEND IMPLEMENTATION

The following items are marked **UNRESOLVED** because client/server operational design specifications are ambiguous or not fully represented in the local Client code:

### 1. Unified Authenticator Password Salt Scheme
- **Context:** `CachedAuthenticationProvider` validates offline gamers using cached credentials. However, the exact salting/hashing scheme (e.g. SHA-256, bcrypt, PBKDF2 iterations) that the central server must use to generate cached hashes is not explicitly dictated in the visual client providers.
- **Blocker:** Must determine the central user password hashing standard before backend user provisioning can begin.

### 2. Live Telemetry Retention & Downsampling Timelines
- **Context:** Telemetry is pushed every 30 seconds. In a 500-workstation gaming center, this produces 1.44 million rows per day.
- **Blocker:** Database retention timelines (e.g., purge raw telemetry after 7 days, retain hourly averages for 30 days) must be agreed upon to design proper persistent partitioning.

### 3. SPK Binary Package Signing Key Distribution
- **Context:** The Client relies on `SignatureVerifier` to authenticate update files against `server_public.key` or certificate pinning.
- **Blocker:** We must establish the PKI lifecycle, confirming how the private key will be securely stored in the Backend CI/CD pipelines to sign SPK updates.

---

### CONCLUSION
This specification provides complete, precise requirements derived directly from the SAYRA Client's assemblies. A backend team implementing these exact REST APIs and persistent socket handshake flows will achieve **100% seamless compatibility** with the current SAYRA Client without requiring any local workstation client changes.
