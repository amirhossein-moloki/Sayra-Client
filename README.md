# Sayra Client

## Overview
Sayra Client is a production-ready Windows Service designed as the client component for the Sayra GameNet management system. It provides remote control, session management, and kiosk lockdown capabilities for PCs in a LAN-based environment. Built on .NET 8, it operates without a user interface and is designed for stability, security, and low resource footprint.

## Architecture Summary
The system follows a modular, service-oriented architecture.
- **Core Engine:** A .NET 8 Windows Service host managing the application lifecycle.
- **Network Layer:** Asynchronous TCP communication using newline-delimited JSON messages.
- **Security Layer:** A hardened transport layer utilizing AES-256-CBC encryption and HMAC-SHA256 signatures for message integrity and confidentiality.
- **State Machine:** A centralized `ClientStateManager` that drives the connection and authentication lifecycle (STARTING, CONNECTING, AUTHENTICATING, READY, IN_SESSION, etc.).
- **Background Services:** Dedicated `HostedServices` for heartbeat reporting, system watchdog, anti-tamper enforcement, and update management.

## System Components
- **TcpClientManager:** Manages persistent TCP connections and handles automatic reconnection logic.
- **MessageHandler & CommandRouter:** Decouples message reception from processing, routing commands to specialized handlers.
- **SessionManager:** Controls the session lifecycle (Start, Stop, Pause, Resume), tracks elapsed time, and persists state to `session_state.json` for crash recovery.
- **KioskManager:** Enforces PC lockdown by managing Windows registry policies (e.g., disabling Task Manager).
- **ProcessManager & ProcessMonitor:** Provides controlled application execution and real-time process tracking.
- **DiagnosticsService:** Collects system metrics (CPU, RAM, Uptime) for remote monitoring.
- **AuthManager:** Implements a secure challenge-response handshake to establish session keys.

## Communication Flow
1. **Connection:** Client establishes a TCP connection to the configured Server IP/Port.
2. **Authentication:**
   - Server sends an `AUTH_CHALLENGE`.
   - Client computes an HMAC-SHA256 signature using the `MasterKey` and generates a new random `SessionKey`.
   - Client sends an `AUTH_RESPONSE` containing the signature and the `SessionKey` (encrypted with the `MasterKey`).
   - Server validates and responds with `AUTH_STATUS`.
3. **Synchronization:** Upon successful authentication, the client sends a state sync message containing its current session status.
4. **Command Execution:**
   - Server sends secured command envelopes.
   - Client validates timestamp (replay protection), signature, and decrypts the payload.
   - Command is routed to the appropriate handler, executed, and a result is returned.

## Installation / Run Instructions

### Prerequisites
- .NET 8.0 Runtime
- Windows 10/11 or Windows Server

### Build
```powershell
dotnet build SayraClient/SayraClient.csproj -c Release
```

### Installation
1. Use the provided `publish.ps1` to create a self-contained release.
2. Run `install.ps1` as Administrator to register the application as a Windows Service.
   - The service is configured with SCM recovery options for automatic restarts on failure.

## Configuration
Configuration is managed via `appsettings.json` and environment variables.
- `ServerConfig`: Defines IP, Port, and timing intervals.
- `SAYRA_MASTER_KEY`: (Environment Variable) The Base64 encoded master key for authentication.
- `UpdateConfig`: URL and intervals for version polling.

## Security Model
- **Authentication:** Challenge-response handshake proves identity without transmitting the MasterKey.
- **Confidentiality:** All command payloads are encrypted using AES-256-CBC with a unique SessionKey per connection.
- **Integrity:** Every message includes an HMAC-SHA256 signature to prevent tampering.
- **Replay Protection:** 10-second UTC timestamp window validation for all secured messages.
- **Persistence Security:** Sensitive session keys are kept in memory only; session state persistence does not include secrets.

## Limitations (Code Reality)
- **Power Management:** Commands for `RESTART_PC`, `SHUTDOWN_PC`, and `LOGOFF` are defined in the specification but are not yet implemented in the `SystemCommandHandler`.
- **Unlock PC:** `UNLOCK_PC` is received but is currently a stub that returns an "not fully implemented" error.
- **Update Process:** `UpdateManager` handles download and checksum verification but requires an external utility to perform the final binary replacement while the service is stopped.
- **Platform:** While core logic is cross-platform, `KioskManager` and `SystemCommandHandler` (Lock PC) rely on Windows-specific APIs (Registry, Win32 API).

## Current Status
- **Implemented:** Session management, Kiosk lockdown (Task Manager), Process control (Run/Kill/List), Diagnostics, Secure Transport, Reconnection logic, State persistence.
- **Stable:** QA validated for up to 500 concurrent clients.
- **Active:** Core functionality is complete and ready for production deployment in LAN environments.

---

### Meta: Documentation Updates

#### What changed from old README
*Note: This repository did not have a root README.md. This file was created to aggregate information from project_spec.md, SYSTEM_AUDIT.md, and source code analysis.*

#### List of removed outdated claims
- Claims regarding full implementation of `SHUTDOWN_PC`, `RESTART_PC`, and `LOGOFF` were removed as they are missing from the command handlers.
- Assumptions about a built-in UI were removed; the client is strictly a background service.
- Clarified that `UpdateManager` performs verification but not yet automated binary replacement.

#### Missing documentation gaps
- **Protocol Spec:** A detailed field-by-field specification of the secure envelope is needed for third-party server implementations.
- **Registry Permissions:** Documentation on the specific permissions required for the Service Account to modify `HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\System`.

#### Codebase Health Summary
The codebase is highly maintainable with a clear separation of concerns. The use of Dependency Injection and the Command pattern makes it easy to extend with new features. Security implementation is robust and follows modern best practices for local network agents.
