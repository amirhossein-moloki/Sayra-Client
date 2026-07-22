# SAYRA Server Development & Implementation Guide

This guide is the authoritative reference for developing, validating, and maintaining the SAYRA Server to support the .NET 8 SAYRA Client. It details the hybrid communication architecture, authentication/authorization pipelines, live session managers, and remote action directives expected by the client.

---

## 1. System Architecture & Transport Layers

The SAYRA Client and Server communicate using a hybrid multi-layer network model designed for discoverability, real-time control, and administration.

```
+------------------------------------------------------------------------+
|                               SAYRA Server                             |
+------------------------------------------------------------------------+
      ^                          ^                          ^
      | UDP Port 37020           | Secure TCP Port 5000     | REST HTTP/S API
      | Server Discovery         | Real-time commands       | Data Sync & Auth
      v                          v                          v
+------------------------------------------------------------------------+
|                               SAYRA Client                             |
+------------------------------------------------------------------------+
```

### Transport Mechanisms:
1. **UDP Auto-Discovery (Port 37020):** Used by clients to dynamically locate servers on local area networks without hardcoded IP configurations.
2. **Persistent Secure TCP Sockets (Port 5000):** Real-time command channel. Messages are framed as newline-delimited (`\n`) JSON structures. Post-handshake communication requires encryption and signatures.
3. **REST HTTP/HTTPS APIs:** Facilitates gamer login, dynamic reservation checks, update manifest polling, advertisement distribution, and delta synchronizations.

---

## 2. Secure TCP Transport & Cryptographic Protocol

Once the raw TCP connection is established on Port 5000, client and server MUST complete the following cryptographic challenge-response handshake:

### 2.1 The Handshake Sequence

```
Client                                                  Server
  |                                                       |
  |<------------------ AUTH_CHALLENGE --------------------| (Plaintext JSON)
  |                                                       |
  |-- AUTH_RESPONSE (Encrypted SessionKey + Sign) ------->| (Plaintext outer)
  |                                                       |
  |<------------------- AUTH_STATUS ----------------------| (Plaintext status)
  |                                                       |
[ Transitions to READY state ]
```

1. **`AUTH_CHALLENGE` (Server -> Client):** The server sends a plaintext JSON containing a random cryptographic challenge nonce.
2. **`AUTH_RESPONSE` (Client -> Server):** The client calculates `HMAC-SHA256(challenge, MasterKey)` as the `response` string. It also generates a random 256-bit AES Session Key, encrypts it using the Server's Public Master Key, and transmits both.
3. **`AUTH_STATUS` (Server -> Client):** The server verifies the signature. On success, it stores the decrypted Session Key in memory, returns a successful `AUTH_STATUS` JSON, and unlocks further communication.

### 2.2 Secure Message Envelope

Post-handshake, all TCP traffic MUST use the `SecureMessageEnvelope` format:

```json
{
  "payload": "AES_256_CBC_ENCRYPTED_JSON_STRING_BASE64",
  "signature": "HMAC_SHA256(SessionKey, payload + timestamp)_HEX",
  "timestamp": "2026-10-18T12:00:05Z"
}
```

* **AES-256-CBC:** Ciphertext is encrypted with PKCS7 padding.
* **Timestamp Replay Protection:** Servers and clients MUST discard any envelope where the UTC timestamp has drifted more than **10 seconds** from the active system time.

---

## 3. Core Flow Lifecycle Models

### 3.1 Authentication & Authorization Flow
```
[Gamer Inputs Credentials]
       |
       v
[AuthenticationService]
       |
       +---> [LocalAdminAuthenticationProvider] (Matches "admin" / "afmin" locally)
       |
       +---> [ServerReservationAuthenticationProvider] (HTTPS / Cache)
       |
       +---> [CachedAuthenticationProvider] (Local Cached Gamers)
       |
       v
[AuthenticationResult]
       |
       +---> (Success) ---> Raise [AuthenticationSucceeded] ---> Unlock Kiosk & Start Timer
       +---> (Failed)  ---> Raise [AuthenticationFailed] ---> Show UI Error Dialog
```

### 3.2 Session Lifecycle & Billing Flow
* **Initiation:** Triggered via user login or a remote `START_SESSION` TCP command. The client loads the session duration, starts decrement timers, and locks the Windows Shell (Kiosk) to authorized limits.
* **Heartbeat & Telemetry:** Every 30 seconds, the client sends a `TELEMETRY_REPORT` containing CPU and RAM loads alongside active process information.
* **Suspension:** A remote `PAUSE_SESSION` halts the timers, while `RESUME_SESSION` restores tracking.
* **Termination:** When duration hits 0 or `STOP_SESSION` is received, the client stops all active launcher processes, writes final metrics to `session_state.json`, and engages the Windows Kiosk Lockout screen.

---

## 4. State Machine Definition

The SAYRA Client operates on a centralized state machine maintained by `ClientStateManager`:

```
      [ STARTING ]
           | (Init completed)
           v
     [ DISCOVERING ] <--------------------+
           | (Discovered server)          |
           v                              |
     [ CONNECTING ]                       |
           | (TCP Handshake OK)           | (Connection Lost)
           v                              |
      [ READY ] --------------------------+
           | (Gamer Login Succeeded)
           v
      [ IN_SESSION ] <--------------------+
           |                              |
           +---> [ LAUNCHING_GAME ]       |
           |         |                    |
           |         v (Game Started)     |
           +---> [ PLAYING ]              |
           |         |                    |
           |         v (Game Crashed)     | (State Recovered)
           +---> [ CRASH_RECOVERING ]     |
           |                              |
           v (Session End / Timeout)      |
     [ ENDING_SESSION ]                   |
           |                              |
           v (Clean-up completed)         |
      [ LOCKED ] -------------------------+
           | (Connection Lost / Failures)
           v
     [ DISCONNECTED ] ---> [ RECOVERING ]
```

---

## 5. Implementation Notes & Known Constraints

1. **Power Management Implementation Limits:** The commands `SHUTDOWN_PC`, `RESTART_PC`, and `LOGOFF` are defined in the network handlers, but physical invocation relies on Windows shell wrappers that must execute as local administrators.
2. **Kiosk Locking (Windows Exclusive):** Kiosk locking commands edit Windows registries (`HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\System`). The Server must assume that clients running under other Operating Systems fall back to dummy/mock overlays.
3. **Persian Right-to-Left Alignment:** All billing and duration payloads formatted for UI binding must accommodate Farsi/Persian numerical displays and Right-to-Left layouts.
4. **Update File Swapping:** The client downloads and validates binary updates via its `UpdateManager` background worker, but final file swapping is delegated to a standalone utility (`Sayra.Client.Updater.exe`) when the client process terminates. The server must expect a momentary connection drop during updates.
5. **Partial / Unknown State Recovery:** If the TCP connection is interrupted during active gaming sessions, the client attempts automatic recovery. If recovery times out, it remains in the active offline game mode and caches final billing delta offsets until connection restoration is successful.
