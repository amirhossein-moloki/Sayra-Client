# SAYRA Enterprise Windows Client — Phase 3: Enterprise Security Hardening Specification
**Title:** Official Enterprise Security Architecture and Specification Document
**Version:** 3.0.0-RTM
**Author:** Principal Enterprise Security Architect, Windows Security Architect, and Technical Specification Writer
**Classification:** Proprietary / Enterprise Confidential
**Target Platform:** .NET 8, Windows Service (Session 0), WPF Shell (Session 1+), Windows 10/11 Workstations

---

## 1 Executive Summary

### 1.1 Purpose
The purpose of this specification is to define the authoritative security architecture, design patterns, cryptographic protocols, local system boundaries, and security components required for the **SAYRA Enterprise Windows Client (Phase 3 — Enterprise Security Hardening)**. This document serves as the absolute, immutable **Single Source of Truth** for future implementation. It is designed to be comprehensive, technical, and sufficiently detailed so that any senior Windows security engineer or advanced AI can implement the entire security subsystem with zero ambiguity and without requiring additional clarification.

### 1.2 Security Objectives
The SAYRA Client operates on public workstations inside gaming centers, cybercafes, and esports arenas. Workstations are physically accessible to public users, and the local threat landscape is highly hostile. Thus, the client must enforce:
*   **Zero Trust Architecture:** Every incoming or outgoing request, inter-process communication message, and file access pattern must be verified, validated, authenticated, and authorized, assuming the local environment is compromised.
*   **Defense in Depth:** Multiple, overlapping layers of technical security controls across user session space (Session 1+), system service space (Session 0), network transport, cryptographic key storage, local physical files, and operating system policies.
*   **Session & Desktop Isolation:** Absolute isolation between the high-privilege Windows Service runner (`NT AUTHORITY\SYSTEM` in Session 0) and the interactive presentation WPF user interface shell (`Sayra.UI` / `Sayra.Client.UI` running in the user’s low-privilege interactive desktop context).
*   **Data Integrity & Authenticity:** Guaranteeing that local configuration records, game databases, billing files, and telemetry events cannot be read, modified, or replayed by malicious users, even with local administrative access.

### 1.3 Threat Model (Hostile Workstation Assumption)
The foundational threat model assumes that **attackers have full physical and local machine access**. They can:
1.  Attempt to reboot into recovery environments, access the local hard drives via live USB sticks, or read physical memory.
2.  Decompile, reverse-engineer, and debug running client binaries.
3.  Inject DLLs, hook system APIs, and dump process memory using administrative tools.
4.  Intercept and spoof network traffic over the LAN (man-in-the-middle attacks) or manipulate the operating system's system clock to steal billing sessions.
5.  Attempt to access or modify local SQLite databases and settings configurations to elevate privileges or bypass kiosk locking.

### 1.4 Business Goals
*   **Prevent Revenue Leakage:** Guarantee that billing countdowns and active sessions are tamper-proof, preventing players from manipulating timers or local caches to play for free.
*   **Secure Digital Licenses & Assets:** Safeguard proprietary launchers, game executables, and game assets from theft, tampering, or extraction of dynamic credentials.
*   **Enable Centralized Enterprise Administration:** Ensure thousands of workstations across various regions can be managed, locked, unlocked, and updated securely with zero-touch management.
*   **Achieve Zero Operational Downtime:** Provide robust, secure offline survival mechanisms that prevent commercial disruption while maintaining strict audit and security boundaries.

### 1.5 Expected Outcomes
Upon complete implementation of this specification, the SAYRA client will achieve the highest grade of local workstation resilience. It will become impossible to bypass kiosk restrictions without triggering hardware-bound security alerts, local data will remain encrypted at rest via hardware-bound cryptographic keys, and all network and inter-process interfaces will validate caller identities using strong cryptographic signatures.

---

## 2 Security Scope

### 2.1 Included (In-Scope Subsystems)
*   **Machine & Client Cryptographic Identity Management:** Hardware-bound workstation identity generation, certificate enrollments, and dynamic challenge-response tokens.
*   **Local Cryptographic Storage Vault:** Implementation of DPAPI (Data Protection API) and SQLCipher AES-256-CBC database schemas for local persistence.
*   **Secure Configuration Synchronization:** Cryptographic signature verification of configuration files (`client_config.json`) using pre-distributed public keys and fail-safe rollback engines.
*   **Transport Layer Hardening:** Mandatory TLS 1.3 network sockets, strict certificate pinning, and cryptographic replay/sequence validation.
*   **Inter-Process Communication (IPC) Security:** Secure Windows Named Pipe architecture (`\\.\pipe\SayraClientIpcPipe`) with tight Discretionary Access Control Lists (DACLs) and caller SID verification.
*   **Workstation Lockout & Kiosk Shell Hardening:** Custom Win32 secure desktops, low-level keyboard hooks, Windows Registry lockdowns, and process termination boundaries using Windows Job Objects.
*   **Anti-Tamper & Code Integrity Guard:** Real-time executable validation (SHA-256 & Authenticode), DLL injection detection, debugger blocking, and memory integrity monitors.
*   **Security Auditing & Windows Event Integration:** Structured tamper, authentication, and administrative event logging routed to protected local storage and Windows Event Logs.

### 2.2 Excluded (Out-of-Scope)
*   **Server-Side Security Implementations:** Hardware firewall configurations, remote cloud SQL server clustering, and cloud provider IAM management (except the strict definitions of APIs the client communicates with).
*   **Physical Station Security:** Workstation case lock specifications, physical anti-theft cabling, or CCTV security monitoring guidelines inside cybercafes.
*   **Operating System Installation Procedures:** Creating custom ISO files, slipstreaming Windows drivers, or managing raw Active Directory Domain Controller installations.

### 2.3 Dependencies
*   **.NET 8 Runtime:** The primary execution framework.
*   **Windows Security APIs & Win32 SDK:** Deep platform integration with `advapi32.dll`, `user32.dll`, `kernel32.dll`, `wtsapi32.dll`, and `bcrypt.dll` (CNG).
*   **SQLCipher / SQLite:** For secure, high-performance local database operations.
*   **Windows DPAPI & CNG (Cryptography Next Generation):** For machine-specific and user-specific cryptographic envelope encryption.
*   **SharpVectors Library:** For safe vector graphic rendering to avoid external XML entity injection.

### 2.4 Success Criteria
1.  **Zero Plaintext Secrets:** Under no circumstance shall database passwords, user tokens, server credentials, or configuration backups be written to disk or stored in physical files in plaintext.
2.  **Zero-Bypass Kiosk:** No standard user can escape the locked WPF shell workspace to access Explorer, Task Manager, Command Prompt, or secondary desktop spaces.
3.  **No Single Point of Trust:** The local workstation does not trust itself; it must authenticate and verify every command and configuration delta arriving from local storage, IPC, or network channels.
4.  **Zero Memory Leakage of Cryptographic Keys:** Cryptographic keys must be pinned in memory and wiped immediately after use, preventing extraction via offline cold-boot or memory dump attacks.

### 2.5 Out of Scope
*   Broad physical security policies.
*   Configuring local BIOS passwords.
*   Network switches/VLAN configuration details at cybercafes.

---

## 3 Security Architecture

```
                                    +-------------------------------------+
                                    |         SAYRA REMOTE SERVER         |
                                    +-------------------------------------+
                                                      ^
                                                      | TLS 1.3 Socket (Port 5000)
                                                      v
+---------------------------------------------------------------------------------------------------------+
| WORKSTATION OPERATING SYSTEM BOUNDARY                                                                   |
|                                                                                                         |
|  +---------------------------------------------------------------------------------------------------+  |
|  | SESSION 0 SYSTEM SPACE (High Privilege: NT AUTHORITY\SYSTEM)                                      |  |
|  |                                                                                                   |  |
|  |   +--------------------------+       +--------------------------+       +----------------------+  |  |
|  |   |    SAYRA WINDOWS SERVICE | <===> |    SECURE NAMED PIPE     | <===> |  SQLCIPHER LOCAL DB  |  |  |
|  |   |    (Core Engine)         |       |    \\.\pipe\SayraIpcPipe |       |  (Offline Queue,     |  |  |
|  |   |    - CNG Core            |       |    - Strong DACL (SYSTEM) |       |   Telemetry, Config) |  |  |
|  |   |    - DPAPI Wrappers      |       |    - Caller SID Validation|       |  - AES-256-CBC Keyed |  |  |
|  |   +--------------------------+       +--------------------------+       +----------------------+  |  |
|  |                 |                                     ^                                           |  |
|  |                 | Spawn process                       | IPC Commands                              |  |
|  |                 | as interactive user                 | & Event updates                           |  |
|  |                 v                                     v                                           |  |
|  +----------------─┼─────────────────────────────────────┼───────────────────────────────────────────+  |
|                    |                                     |                                              |
|  +─────────────────v─────────────────────────────────────v───────────────────────────────────────────+  |
|  | SESSION 1+ USER INTERACTIVE SPACE (Low Privilege: Interactive User)                               |  |
|  |                                                                                                   |  |
|  |   +--------------------------------------------------------------------------------------------+  |  |
|  |   |   SAYRA WPF SHELL (Sayra.UI / Sayra.Client.UI)                                             |  |  |
|  |   |   - Intercepts Keyboard Hooks (WH_KEYBOARD_LL)                                             |  |  |
|  |   |   - Launches isolated "SayraSecureDesktop" using Win32 Desktop API                         |  |  |
|  |   |   - Binds UI ViewModels using DPAPI-secured session contexts                               |  |  |
|  |   +--------------------------------------------------------------------------------------------+  |  |
|  |                                                 |                                                 |  |
|  |                                                 v Job Object Parent                               |  |
|  |                                   +───────────────────────────+                                   |  |
|  |                                   |  LAUNCHED GAME PROCESSES  |                                   |  |
|  |                                   |  - Constrained Affinity   |                                   |  |
|  |                                   +───────────────────────────+                                   |  |
|  +---------------------------------------------------------------------------------------------------+  |
+---------------------------------------------------------------------------------------------------------+
```

### 3.1 Architectural Trust Boundaries
1.  **Network Boundary (Workstation ↔ Remote Server):** Transition from untrusted public LAN networks to the local secure socket context. Enforces TLS 1.3 with certificate pinning.
2.  **Session Isolation Boundary (Session 0 ↔ Session 1+):** Transition from high-privilege administrative system level (`LocalSystem`) to low-privilege visual interactive user space. Spawns visual client via `CreateProcessAsUser` using authenticated desktop tokens.
3.  **Local Storage Boundary (Process Memory ↔ Hard Drive):** Transition from transient protected RAM state to persistent storage. All written data must traverse DPAPI or SQLCipher cryptographic envelopes.
4.  **IPC Boundary (Service Process ↔ WPF Process):** Communication over local Named Pipes. Caller validation is executed on every write/read sequence via Win32 Security Descriptors.

### 3.2 Security Zones
*   **Zone Red (Untrusted Area):** The raw physical interface, standard USB ports, external local network adapters, and untrusted user process spaces (such as cheat tools or web browsers running inside Session 1).
*   **Zone Amber (Restricted Area):** Interactive WPF shell context (`Sayra.UI`). Highly hardened, utilizing low-level Win32 hooks and isolated desktop threads, but running inside user session context.
*   **Zone Green (Trusted Area):** Session 0 Windows Service engine and local SQLCipher database vaults. Runs under `NT AUTHORITY\SYSTEM`, inaccessible to standard user processes.

### 3.3 Privilege Separation (Least Privilege)
The SAYRA architecture rigidly follows the principle of privilege separation:
*   **Core Windows Service (Session 0):** Executes with highest local authority. Responsible for raw process creation, registry modifications, hardware monitoring, code signing validation, and database storage writes. It has zero user interface code.
*   **WPF Client Shell (Session 1+):** Executes with the lowest interactive user privileges. It cannot modify machine-wide configurations, registry keys, or terminate system processes directly. It requests administrative actions exclusively by submitting structured command payloads over the secure IPC Named Pipe to Session 0.

### 3.4 Zero Trust Implementation Model
Every component is designed under the assumption that neighboring components are compromised:
*   The Windows Service does not trust the WPF UI. It verifies the validity, session signature, and token parameters of every command received over the Named Pipe.
*   The local database does not trust the file system. It verifies block integrity using SQLCipher's native cryptographic MAC checks on every database read transaction.
*   The system configuration does not trust local physical files. The configuration manager validates SHA-256 signature hashes against a server-bound public key before applying settings updates.

---

## 4 Security Components

### 4.1 Identity Protection

#### 4.1.1 Machine Identity
Every physical terminal enrolled in the SAYRA client network must generate a unique, cryptographically secure machine identity on initial registration.
*   **Generation Mechanism:** On installation, the service reads the motherboard’s UUID (`WMI Win32_ComputerSystemProduct.UUID`), CPU serial numbers, and primary MAC address. These components are combined, salted with a hardcoded operational vector, and hashed via SHA-256.
*   **Entropy Injection:** This generated hash is used to compile a machine-specific configuration file locked via DPAPI Machine-Store flags, ensuring the identity is non-spoofable and unique.

#### 4.1.2 Client Identity
*   **Session Token Isolation:** Upon player logon, the server generates an cryptographically random UUID Session ID, signed with the server's private key. The client stores this session token in memory (never written to plaintext config files).
*   **Binding:** The user’s identity is cryptographically bound to the current workstation Session ID and the machine's hardware fingerprint on every outbound network message block.

#### 4.1.3 Certificate Management
*   **Local Trust Store Exclusion:** The client must bypass the default Windows Certificate Store for server authentication to prevent attackers from installing rogue Root CA certificates in their local OS.
*   **Certificate Pinning Engine:** The client bundle contains a pinned public-key certificate structure (`server_public.key`). The socket connection validates the remote server’s certificate chain strictly against this pinned signature.

#### 4.1.4 Device Registration
*   **Provisioning Handshake:** New terminals submit a signed enrollment request containing physical system metrics (CPU, BIOS, MAC addresses) and a newly generated local RSA public key.
*   **Acceptance:** The server approves the registration, logs the hardware fingerprint, and issues a workstation identity token, establishing the Zero Trust baseline.

#### 4.1.5 Authentication Tokens
*   All temporary login and API access tokens are stored in protected memory structures using standard .NET secure memory wrappers (e.g., `SecureString` or memory-pinned byte arrays) and encrypted with user-context DPAPI before being buffered.

#### 4.1.6 Session Security
*   **Timeout & Heartbeat Alignment:** Active user sessions require continuous, bi-directional socket heartbeats. If a socket disconnection is detected, the client transitions to offline-grace-countdown mode and forcefully invalidates active session tokens upon timer expiration.

---

### 4.2 Cryptography

#### 4.2.1 DPAPI (Data Protection API)
*   **Application-Level Envelopes:** DPAPI is used to encrypt local configuration files and local DB key material.
*   **Cryptographic Flags:**
    *   **`CRYPTPROTECT_LOCAL_MACHINE`:** Used for configuration data that must be accessible to any process running on the specific workstation.
    *   **`CRYPTPROTECT_UI_FORBIDDEN`:** Standardized across all calls to prevent the operating system from spawning default prompt dialogs during background operations.

#### 4.2.2 SQLCipher (Database Encryption)
*   All persistent SQLite databases (`offline_queue.db`, `game_library.db`, `telemetry_buffer.db`) must be encrypted at the database engine level.
*   **Cryptographic Algorithm:** AES-256-CBC.
*   **Key Derivation:** PBKDF2 with 64,000 iterations using SHA-256.
*   **Key Source:** The database encryption key is generated dynamically by combining a machine-specific DPAPI secret block with the hardware-bound identity fingerprint.

#### 4.2.3 AES, RSA, & ECDSA
*   **AES-256-GCM:** Mandated for all symmetric payload encryption sequences (for both network and local IPC channels).
*   **RSA-4096:** Used for asymmetric registration handshakes and initial key exchanges.
*   **ECDSA-P384:** Standardized for message and file signature verifications due to its superior performance bounds and robust security properties.

#### 4.2.4 Key Storage & Lifetime
*   **Ephemeral Sockets:** Network session keys are ephemeral, generated via Elliptic Curve Diffie-Hellman (ECDH) on handshake, stored in pinned memory, and overwritten with zeros (`CryptProtectMemory` / `SecureZeroMemory`) immediately upon connection termination.
*   **Storage Limits:** Under no circumstances are raw private keys or DB encryption passwords allowed to persist in plaintext in RAM or on disk.

---

### 4.3 Secure Configuration

#### 4.3.1 Configuration Encryption
The primary configuration file `client_config.json` is encrypted at rest using DPAPI (`CRYPTPROTECT_LOCAL_MACHINE` flags) with additional programmatic entropy:
*   **Programmatic Entropy:** A static 32-byte cryptographically random salt array embedded inside the core background binaries.

#### 4.3.2 Integrity Verification
*   **Double Signature Validation:** The configuration manager verifies the configuration database signature against the server’s public key.
*   **Heuristic Validation:** Schema structures are parsed for boundaries (e.g., verifying that `RatePerHour` is a non-negative number and that `UdpPort` is within valid ranges `1024-65535`).

#### 4.3.3 Secure Loading & Recovery Rollback
```
                                [Startup Load Config]
                                          |
                                          v
                              [Read encrypted file]
                                          |
                                          v
                              [DPAPI Decrypt Operation]
                                          |
                                          v
                              [Validate Schema & Signatures]
                                          |
                        +-----------------+-----------------+
                        | (Success)                         | (Fail/Corrupt)
                        v                                   v
             [Apply Local Profile]               [Restore config.json.bak]
                        |                                   |
                        v                                   v
             [Start Workstation]                 [Enforce Validation Check]
                                                            |
                                                            +---> (Pass) ---> [Apply Backup]
                                                            +---> (Fail) ---> [Hard Lockout]
```

*   **Failsafe Swap:** When a new configuration is received via the sync channel, it is written to `client_config.json.tmp`. Only after the temporary file passes complete signature and schema validation is it swapped to overwrite `client_config.json`, keeping `client_config.json.bak` as a safe fallback.
*   **Lockdown Fallback:** If both primary and backup configurations are corrupted or compromised, the system blocks UI shell activation, writes a critical warning to the Windows Event Log, and triggers an emergency hardware lockout.

---

### 4.4 Secure Communication

#### 4.4.1 TLS 1.3
*   All client-server TCP socket interactions must enforce TLS 1.3.
*   **Cipher Suite Restrictions:** Only the following high-strength cryptographic cipher suites are permitted:
    *   `TLS_AES_256_GCM_SHA384`
    *   `TLS_CHACHA20_POLY1305_SHA256`
*   Older protocols (TLS 1.0, 1.1, 1.2) are explicitly blocked.

#### 4.4.2 Certificate Pinning
*   The client implements a custom SSL validation callback (`RemoteCertificateValidationCallback`). The callback extracts the public key of the server’s certificate and executes a bitwise comparison against the pre-compiled `server_public.key` SHA-256 fingerprint, completely ignoring local root store trusts.

#### 4.4.3 Replay Protection & Message Authentication
*   **Monotonic Sequence Numbers:** Every network message block must include a monotonic, incrementing sequence counter. The receiver tracks sequence history, immediately rejecting any message containing a sequence ID equal to or lower than the previous highest count.
*   **Cryptographic Nonces:** High-priority commands (e.g., remote unlock or restart commands) require a unique, one-time 16-byte nonce issued by the client in a challenge sequence.
*   **Dynamic Time Verification:** Messages must embed an ISO 8601 UTC timestamp. The receiver validates that the message timestamp is within a maximum of 5 seconds of the client’s synchronized NTP time, discarding stale frames.

---

### 4.5 IPC Security

#### 4.5.1 Named Pipe ACLs (Access Control Lists)
The inter-process communication Named Pipe (`\\.\pipe\SayraClientIpcPipe`) must be created with a highly restrictive Windows Security Descriptor:
*   **System Access Only:** Deny all generic access. Add explicit Access Control Entries (ACEs) allowing only:
    *   `NT AUTHORITY\SYSTEM` (Full Control)
    *   The Active User’s Interactive Security Identifier (SID) (Read, Write, and Synchronize permissions).
*   This prevents other local user processes, sandboxed applications, or non-interactive background accounts from connecting to the IPC pipe.

#### 4.5.2 Pipe Authentication & Privilege Verification
*   **Caller Validation:** Upon connection, the pipe server calls the Win32 API `GetNamedPipeClientProcessId` to determine the caller's Process ID.
*   **Token Verification:** Using the PID, the server opens the caller process handle (`OpenProcess` with `PROCESS_QUERY_INFORMATION`) and extracts its security token (`OpenProcessToken`). The server verifies that the process SID exactly matches the currently authorized active user session SID, blocking administrative access attempts from the low-privilege visual application.

#### 4.5.3 Impersonation Boundaries
*   **Impersonation Level Prevention:** The Named Pipe server explicitly sets the security quality of service to block impersonation (`SecurityIdentification`). Under no circumstance is the service permitted to delegate or elevate its token context based on visual application identity requests.

---

### 4.6 Local Storage Protection

#### 4.6.1 Secrets & Credential Protection
*   All stored administrative passwords (such as the bypass keys inside `local_admin.json`) are hashed using PBKDF2 with SHA-256, applying unique 32-byte salts, and then wrapped inside DPAPI Machine-Store envelopes.
*   Active session tokens, database key materials, and diagnostic credentials are stored exclusively in non-pageable memory blocks using `SecureString` structures.

#### 4.6.2 Offline Queue Encryption
The database cache storage containing offline events and transactions is heavily protected:
*   **Encryption Engine:** SQLCipher with 256-bit AES-CBC.
*   **Compression:** Data rows are serialized and compressed via GZip prior to SQLCipher encryption.
*   **Integrity Verification:** A 32-byte HMAC signature is appended to each stored queue row, verifying record sequential chaining.

---

### 4.7 Anti-Tamper

#### 4.7.1 Executable & DLL Validation
*   **Authenticode Signature Enforcement:** On startup, and before launching any administrative tool or updater executable, the client validates the binary's digital signature using the Win32 `WinVerifyTrust` API. Unsigned or self-signed executables are immediately quarantined.
*   **Dynamic Hash Scanning:** Crucial operational DLLs (such as cryptographic helpers or network wrappers) are mapped against an internal manifest containing their SHA-256 hashes. The system runs background check loops, immediately shutting down if a critical DLL hash drift is detected.

#### 4.7.2 Memory Integrity & Debugger Blocking
*   **System Debug Hooks:** Integrates standard Win32 debugging checks (`IsDebuggerPresent`, `CheckRemoteDebuggerPresent`) alongside low-overhead hardware-breakpoint registers monitoring.
*   **Process Injection Interception:** Monitors the workstation process handle count. If unexpected memory mapping operations (`VirtualAllocEx`, `WriteProcessMemory`, or `CreateRemoteThread`) target the client’s process space, the system initiates an instant emergency lock sequence.

---

### 4.8 Windows Security

#### 4.8.1 Windows Defender Integration
*   The SAYRA installer automatically registers the core background binaries to the local Windows Defender exclusion lists to prevent anti-virus hooks from blocking low-level keyboard hook registrations or process scanning activities.

#### 4.8.2 Windows Event Log Security
*   Critical security audits, kiosk violations, database repair sequences, and administrative overrides are written to the custom Windows Event Log channel (`Applications and Services Logs` -> `SAYRA_Client`). Access permissions are secured to allow only `SYSTEM` and administrators to purge logs.

#### 4.8.3 Credential Guard & Secure Boot Compatibility
*   **Hardware Protections Compatibility:** The client's cryptographic engine relies strictly on hardware-backed keys. It is designed to run compatibly alongside Windows Credential Guard and Secure Boot environments.

#### 4.8.4 Code Signing & Protected Processes
*   All distributed executable packages are signed with an enterprise-grade digital certificate. The service is prepared for deployment as a Windows Protected Process (PPL) to protect it from being killed by standard user processes or custom task killers.

---

### 4.9 Audit Security

#### 4.9.1 Audit Event Classifications
The client monitors and records events across five distinct security domains:
1.  **Security Events:** Low-level hook violations, unauthorized USB insertions, debugger attachments, and system clock manipulation attempts.
2.  **Audit Events:** Transactions, game launches, billing countdown milestones, and session transitions.
3.  **Authentication Logs:** Local admin login bypasses, failed credential entries, and token generation steps.
4.  **Administrative Actions:** Settings overrides, remote reboot commands, and local backup restorations.
5.  **Tamper Detection Alerts:** Code signature verification failures, SQLite file validation anomalies, and config schema violations.

#### 4.9.2 Tamper-Proof Audit Logging
*   Audit logs are written to an encrypted SQLCipher table (`security_audit`). Every logged audit row includes a dynamic SHA-256 verification hash:
    $$\text{RowHash} = \text{SHA256}(\text{EventID} + \text{Timestamp} + \text{Payload} + \text{PreviousRowHash})$$
*   This establishes a cryptographic hash chain. If a user tries to delete or modify a log entry manually via external SQL tools, the chain validation fails on the next service transaction, triggering an instant system alert.

---

## 5 Threat Model (STRIDE Methodology)

### 5.1 STRIDE Threat Modeling & Mitigations

| STRIDE Category | Specific Workstation Threat | Threat Level | Architectural Mitigation Strategy |
| :--- | :--- | :--- | :--- |
| **Spoofing** | An attacker mimics the master server to send unauthorized remote unlock commands. | **CRITICAL** | All incoming network frames require Elliptic Curve Digital Signatures (ECDSA-P384) verified against the pinned `server_public.key`. |
| **Tampering** | User modifies `client_config.json` to disable Kiosk locking or set their station identity to another active PC ID. | **CRITICAL** | Configuration is encrypted via DPAPI with custom entropy. Signature verifications and schema boundary checks are performed on every file load. |
| **Repudiation** | User claims they did not initiate a billing event or launch a paid game application. | **HIGH** | Sequential, cryptographic hash-chained transaction logs are written to a SQLCipher encrypted database, locked via machine-specific hardware keys. |
| **Information Disclosure** | Attacker decompiles binaries or dumps memory to extract the SQLCipher database encryption password. | **CRITICAL** | DB encryption keys are dynamically constructed by combining machine-specific DPAPI blocks with motherboard UUIDs, ensuring the key never exists statically in files or files. |
| **Denial of Service** | User floods Named Pipes or UDP Ports with junk packets to crash client listeners. | **MEDIUM** | Pipe DACLs restrict generic access to the system account and active user interactive SID. Malformed payloads are immediately dropped prior to parsing. |
| **Elevation of Privilege** | User escapes the WPF shell, opens Command Prompt, and attempts to stop the SAYRA background service. | **CRITICAL** | The service runs in Session 0 as `LocalSystem`, protected from interactive standard users. The visual UI runs in low-privilege context, unable to stop or modify the service. |

---

## 6 Security Models

### 6.1 Workstation State Security Model

#### 6.1.1 Purpose
Ensures that the workstation transitions only between valid security states, preventing unauthorized desktop exposure or session hijacking.

#### 6.1.2 Properties
*   **State Enum:** `STARTING`, `DISCOVERING`, `CONNECTING`, `READY`, `IN_SESSION`, `PLAYING`, `MAINTENANCE`, `DISCONNECTED`.
*   **Atomic State Machine:** Backed by a strict state machine validator (e.g., Stateless library) that enforces transition permissions.
*   **Immutable Lock:** The lock state is enforced via a low-level secure desktop thread and continuous keyboard hook loops.

#### 6.1.3 Validation
*   Transition from `READY` to `IN_SESSION` requires a cryptographically verified `AuthenticationResult` containing active player credentials or dynamic server reservation authorization tokens.

#### 6.1.4 Lifecycle
*   Instantiated on system boot. Persistent throughout machine runtime. Terminated on operating system shutdown.

---

### 6.2 Cryptographic Key Security Model

#### 6.2.1 Purpose
Manages the generation, runtime storage, usage scope, and secure zeroing of cryptographic key material.

#### 6.2.2 Properties
*   **Key Isolation:** No raw cryptographic keys (network or storage keys) are permitted to exist in the global Garbage Collector (GC) heap as standard strings.
*   **Secure Memory Wrapper:** Key byte arrays are pinned in physical memory using Win32 API functions (`VirtualLock`) and encrypted via DPAPI memory-protection flags.

#### 6.2.3 Validation
*   On decryption tasks, key arrays are unwrapped in-memory, utilized immediately, and systematically overwritten using `SecureZeroMemory` or `CryptProtectMemory` block zeroing.

#### 6.2.4 Lifecycle
*   Ephemeral keys exist exclusively during active TCP socket sessions. DB keys are retrieved on startup and discarded upon database session closing.

---

### 6.3 Secure Desktop Security Model

#### 6.3.1 Purpose
Creates an isolated Windows Desktop context to present the SAYRA interface, separating the user workspace from administrative controls.

#### 6.3.2 Properties
*   **Desktop Switcher:** Uses the Win32 `CreateDesktop` API to spin a dedicated desktop named `SAYRA_SECURE_DESKTOP`.
*   **UI Isolation:** The WPF application is spawned within this isolated desktop context. Traditional explorer shell services (such as the taskbar, start menu, or standard Windows keyboard shortcuts) do not exist in this isolated visual space.

#### 6.3.3 Validation
*   The system periodically validates the active desktop handle (`GetThreadDesktop`). If the active desktop shifts away from `SAYRA_SECURE_DESKTOP` without authorized administration bypass credentials, it forces a desktop correction switch loop.

#### 6.3.4 Lifecycle
*   Initialized on ready state. Destroyed upon administrator-approved system maintenance mode transition.

---

## 7 Interfaces

Every security-related component must implement a dedicated public interface to support Clean Architecture, loose coupling, and robust dependency injection.

```csharp
namespace Sayra.Client.Shared.Interfaces.Security
{
    /// <summary>
    /// Governs local hardware-bound credential encryption and secret storage envelopes.
    /// </summary>
    public interface ICryptographyService
    {
        byte[] EncryptWithDpapi(byte[] plaintext, bool useMachineStore, byte[]? optionalEntropy = null);
        byte[] DecryptWithDpapi(byte[] ciphertext, bool useMachineStore, byte[]? optionalEntropy = null);
        byte[] EncryptAesGcm(byte[] plaintext, byte[] key, byte[] nonce, byte[] associatedData);
        byte[] DecryptAesGcm(byte[] ciphertext, byte[] key, byte[] nonce, byte[] associatedData);
        bool VerifySignature(byte[] data, byte[] signature, byte[] publicKey);
    }

    /// <summary>
    /// Enforces local system shell locks, keyboard hooks, and secure desktops.
    /// </summary>
    public interface IKioskSecurityService
    {
        Task EnableKioskLockdownAsync();
        Task DisableKioskLockdownAsync();
        bool IsKeyboardShortcutBlocked(int virtualKeyCode, int modifiers);
        void SpawnSecureDesktop();
        void ReleaseSecureDesktop();
    }

    /// <summary>
    /// Validates executable integrity and dynamic signature chains.
    /// </summary>
    public interface IIntegrityValidator
    {
        bool VerifyAuthenticodeSignature(string filePath);
        string ComputeSha256Hash(string filePath);
        bool ValidateDllIntegrity(string dllName);
    }

    /// <summary>
    /// Controls access policies and DACL security descriptors for Named Pipes.
    /// </summary>
    public interface ISecureIpcPolicyManager
    {
        void ApplyNamedPipeDacl(string pipeName);
        bool ValidatePipeCallerSid(int callerPid);
    }
}
```

### 7.1 Responsibilities
*   `ICryptographyService`: Encapsulates all DPAPI and low-overhead AES-GCM operations.
*   `IKioskSecurityService`: Coordinates Windows secure desktop switching, explorer termination, and low-level global hooks.
*   `IIntegrityValidator`: Verifies target binaries against digital certificates and baseline SHA-256 manifests.
*   `ISecureIpcPolicyManager`: Sets ACL permissions on local Named Pipes and verifies caller process identity contexts.

### 7.2 Thread Safety
*   All implementations of these interfaces **MUST be fully thread-safe**. Background tasks, WMI diagnostics threads, and UI presenters will invoke these operations concurrently from multiple task pool threads.

### 7.3 Expected Implementations & Dependency Injection Lifetimes
*   `CryptographyService` (Interface: `ICryptographyService`): Standard implementation utilizing CNG (`bcrypt.dll`) and .NET's `ProtectedData` framework.
    *   **Lifetime:** **Singleton**. Cryptographic processing contexts and dynamic salt matrices are maintained globally across the application execution lifecycle to avoid allocation overhead.
*   `KioskSecurityService` (Interface: `IKioskSecurityService`): Deep Win32 SDK implementation utilizing `SetWindowsHookEx` and `CreateDesktop` interops.
    *   **Lifetime:** **Singleton**. Windows hook handles and secure desktop pointers must persist for the entire session duration; scoping this as transient would lead to orphaned OS handles and unhooked system threads.
*   `IntegrityValidator` (Interface: `IIntegrityValidator`): Signature checks utilizing native standard Crypt32 and `WinVerifyTrust` APIs.
    *   **Lifetime:** **Transient** or **Singleton**. Registering as **Singleton** is preferred to permit continuous caching of verified executable hash registries.
*   `SecureIpcPolicyManager` (Interface: `ISecureIpcPolicyManager`): Named pipe DACL manager utilizing security descriptor interops.
    *   **Lifetime:** **Singleton**. Bound to the IPC server host pipeline to continuously validate connecting clients.

---

## 8 Security Flows

### 8.1 User Authentication Flow
1.  **Credential Entry:** Player inputs username/password in the Persian dark-themed WPF screen.
2.  **Service Transition:** UI submits credentials via a JSON command over the Named Pipe to the Windows Service in Session 0.
3.  **Authentication Chain:** Session 0 routes credentials through the Authentication Providers.
4.  **Network Query:** If online, the server is queried using encrypted TLS 1.3 payload frames.
5.  **Token Enrollment:** Server returns successful validation along with an authenticated player token.
6.  **Session Unlocking:** Session 0 initiates a Named Pipe command to notify the WPF UI to unlock the gaming library.

---

### 8.2 Workstation Startup & Lockdown Flow
```
[BIOS/Power On]
       │
       ▼
[SCM Launches SAYRA Service in Session 0]
       │
       ▼
[Verify Executable and DLL Signatures] ─── (Corrupted) ───► [Hard Lockout]
       │
       ▼ (Valid)
[Load encrypted config.json via DPAPI] ─── (Failure) ───► [Restore Backup Config]
       │
       ▼ (Loaded)
[Verify config SHA-256 signature]
       │
       ▼ (Verified)
[Create secure Named Pipe \\.\pipe\SayraClientIpcPipe]
       │
       ▼ (DACL Applied)
[Initialize global keyboard hook (WH_KEYBOARD_LL)]
       │
       ▼ (Hooks Active)
[Switch Thread to SAYRA_SECURE_DESKTOP]
       │
       ▼ (Desktop Isolated)
[Spawn Sayra.UI (WPF) as current Interactive User]
       │
       ▼
[Display Persian RTL Login Interface]
```

---

### 8.3 Handshake & Key Exchange Flow
1.  **Connection:** Client establishes socket connection to Remote Server on Port 5000.
2.  **Challenge Frame:** Server dispatches a 32-byte dynamic `AUTH_CHALLENGE` nonce.
3.  **Diffie-Hellman Exchange:** Client generates an Elliptic Curve Diffie-Hellman (ECDH) key pair.
4.  **Signature Generation:** Client signs the challenge payload + client public key using the client’s ECDSA private key.
5.  **Response Frame:** Client sends signed DH public key to server (`AUTH_RESPONSE`).
6.  **Key Agreement:** Server verifies the signature, agrees on the shared secret, and derives symmetric AES-GCM session keys.
7.  **Symmetric Transition:** All subsequent socket packages use AES-256-GCM encryption with dynamic HMAC signatures.

---

### 8.4 Certificate Validation Flow
1.  **Server Hello:** Server presents its SSL/TLS certificate chain during TLS handshake.
2.  **Callback Interception:** Client’s custom certificate validation callback intercepts the validation step.
3.  **Fingerprint Extraction:** Extract public key bytes from the server's primary certificate.
4.  **Signature Match:** Performs SHA-256 hash of the public key and matches it against the hardcoded compile-pinned `server_public.key` hash.
5.  **Access Determination:** If matching, connection is approved. If there is a mismatch, the connection is instantly aborted.

---

### 8.5 Message Validation Flow
1.  **Frame Arrival:** Sockets read transport package array.
2.  **Timestamp Expiry Verification:** Extract message timestamp and reject if timestamp is older than 5 seconds from system NTP baseline.
3.  **Sequence Number Verification:** Check sequence ID and reject if sequence ID has been replayed.
4.  **HMAC Signature Verification:** Verify HMAC-SHA256 signature against decrypted dynamic session key.
5.  **Payload Decryption:** Decrypt transport payload via AES-256-GCM.
6.  **Routing:** Map deserialized JSON payload context to internal command executors.

---

### 8.6 Configuration Loading & Verification Flow
1.  **Load Trigger:** Service startup or dynamic server sync signal.
2.  **File Reading:** Read raw bytes from `Data/client_config.json`.
3.  **DPAPI Decryption:** Decrypt file bytes using DPAPI with custom static entropy.
4.  **Hash Verification:** Extract embedded signature, verifying it against `server_public.key` using ECDSA-P384.
5.  **Property Validation:** Parse JSON and enforce constraint boundaries.
6.  **Memory Application:** Load configuration properties into active runtime memory buffers.

---

### 8.7 Offline Queue Access Flow
1.  **Enqueuing:** Component dispatches event payload.
2.  **Signature Chain:** Enqueuing service generates current row hash, signing it with the previous row hash.
3.  **SQLCipher Write:** Write structured GZipped binary payload to local SQLite database with AES-256 encryption.
4.  **Dequeue Loop:** Background task reads oldest sequential records from the database.
5.  **Dynamic Verification:** Verify RowHash integrity.
6.  **Transmission:** Dispatch batch payload over secure TLS socket.
7.  **Purging:** Delete database records after confirmed server receipt.

---

### 8.8 Administrative Command Validation Flow
1.  **Command Frame Arrival:** Secure socket receives control command (e.g., `SHUTDOWN` or `UNLOCK`).
2.  **Privilege Evaluation:** Parse incoming identity token and verify that the issuer’s role maps to authorized administrator SIDs.
3.  **Challenge Verification:** Request admin password verification or dynamic multi-factor validation.
4.  **Local Execution:** Service executes administrative action (such as invoking OS shutdown or releasing registry lockdowns).

---

### 8.9 State Recovery Flow
1.  **Reconnection:** Client recovers dropped TCP connection.
2.  **Challenge-Response:** Completes standard dynamic socket handshake.
3.  **Status Dispatch:** Client sends localized station snapshot (Active PID, Session ID, timer remaining minutes) to server.
4.  **Conflict Resolution:** Server reviews state. If state matches server logs, connection continues. If a conflict is detected, the server forces dynamic state correction on the terminal.

---

## 9 Reliability

### 9.1 Security Failure Handling
*   **Failsafe State Principles:** The client is designed around a "Failsafe Close" principle. If a security component fails (e.g., the low-level keyboard hook unregisters, the SQLCipher database locks, or the Named Pipe disconnects), the system must immediately terminate active game sessions and activate the secure locked desktop shell to prevent commercial bypasses.

### 9.2 Certificate Expiration Recovery
*   **Dynamic Certificate Update Channel:** If the pinned certificate is expiring, the server can issue an signed certificate renewal command. The package contains a newly signed `server_public.key` signed by the previous valid certificate's private key. The client verifies the signature chain and swaps the pinned public key array inside protected storage.

### 9.3 Key Rotation Procedures
*   **Network Session Key Rotation:** Network symmetric session keys are rotated automatically every 3,600 seconds or after 100,000 packets are processed, whichever occurs first, to prevent statistical cryptographic analysis attacks.
*   **Storage Master Key Rotation:** Database master keys are rotated when the workstation transitions into maintenance mode, using SQLCipher’s native `rekey` operations.

### 9.4 Corrupted Configuration Recovery
*   **Redundant Profiles:** If the master configuration file is corrupted, the client restores the baseline profile (`config.json.bak`). If the backup is also corrupted, the service locks down the workstation and triggers a critical alarm over UDP to any local administrator terminal.

### 9.5 Compromised Database Recovery
*   **Automated Quarantining:** If a SQLCipher database file fails to open or triggers block integrity errors (indicating raw file tampering), the service immediately renames the corrupted file (`offline_queue.db.corrupted`), creates a clean database schema from compiled manifests, and initiates a repair process.

---

## 10 Performance

Security protocols must be optimized to run with zero visual impact on gaming frame rates, keeping latency low even on budget gaming rigs.

### 10.1 Cryptographic Cost Matrix

| Cryptographic Task | Standard Algorithm | CPU Utilization Limit | Latency Target | Execution Frequency | Optimization Strategy |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Network Encrypt** | AES-256-GCM | < 0.1% | < 1 ms | Every socket write | Use native hardware assembly acceleration (AES-NI) |
| **Signature Check** | ECDSA-P384 | < 0.2% | < 5 ms | On connection / update | Run asymmetric verification exclusively on thread pool |
| **DB Encryption** | SQLCipher AES-CBC | < 0.3% | < 2 ms | Every database write | Increase page cache size to minimize disk writes |
| **File Decryption** | DPAPI | < 0.1% | < 5 ms | Once on startup | Cache values in-memory; avoid frequent physical reads |

### 10.2 Caching Strategies
*   **Certificate & Key Cache:** Ephemeral cryptographic session keys and validated server certificate metadata are cached in pinned memory buffers to avoid repeated handshake computations.
*   **Schema Property Caching:** Decoded configuration settings are parsed once on startup and held in read-only thread-safe structures in RAM, eliminating disk access.

### 10.3 Resource Consumption Limits
*   **Memory Footprint:** The complete security subsystem (including DPAPI, memory-pinned secure strings, and the SQLite connection) must consume **less than 15 MB of RAM**.
*   **CPU Overhead:** Average CPU utilization for security processing must remain **under 0.5%** during active gameplay.

---

## 11 Windows Integration

```
+-----------------------------------------------------------------------------------+
|                            WINDOWS SECURITY ARCHITECTURE                          |
+-----------------------------------------------------------------------------------+
        │                      │                     │                      │
        ▼ (DPAPI)              ▼ (CNG / CryptoAPI)   ▼ (Named Pipe Security)▼ (WTS Session)
┌──────────────┐       ┌──────────────┐      ┌──────────────┐       ┌──────────────┐
│ crypt32.dll  │       │ bcrypt.dll   │      │ advapi32.dll │       │ wtsapi32.dll │
│ - Protected  │       │ - AES-GCM    │      │ - Custom ACL │       │ - Interactive│
│   Storage    │       │ - ECDSA/DH   │      │   Descriptors│       │   Session Tok│
└──────────────┘       └──────────────┘      └──────────────┘       └──────────────┘
```

### 11.1 Native Platform Integrations (P/Invoke & Win32 APIs)
*   **DPAPI Integration:** Utilizes native interops targeting `crypt32.dll` (`CryptProtectData` and `CryptUnprotectData`) to secure local configuration files at-rest.
*   **CNG (Cryptography Next Generation):** Maps directly to `bcrypt.dll` for standard hardware-accelerated AES-GCM and Elliptic Curve Diffie-Hellman operations, bypassing slow managed frameworks.
*   **CryptoAPI / Certificates:** Enforces Authenticode digital signature verifications targeting native Crypt32 APIs for binary verification.
*   **Windows Event Log:** Binds to native event provider frameworks via `System.Diagnostics.EventLog` to log security violations directly to the host OS.
*   **LSA (Local Security Authority):** Binds to LSA session notification events to track Windows logon sequences, allowing instant workstation locker lock operations.
*   **Named Pipe Security:** Uses security descriptors in `advapi32.dll` (`InitializeSecurityDescriptor`, `SetSecurityDescriptorDacl`) to enforce ACL boundaries.
*   **SCM (Service Control Manager):** The background service registers with SCM to run as a high-privilege `LocalSystem` service, ensuring it cannot be terminated by standard interactive users.
*   **WTS (Windows Terminal Services):** Integrates with `wtsapi32.dll` to listen to terminal user session lock/unlock and session change notifications, enabling clean visual shell switches.

---

## 12 Configuration

This section defines the precise programmatic security policies that govern the client runtime settings.

### 12.1 Dynamic Security Policies

```json
{
  "SecurityPolicies": {
    "EnforceZeroTrust": true,
    "BlockUnsignedBinaries": true,
    "AllowedIpcUserGroupSid": "S-1-5-32-545",
    "MaxIpcMessageLengthBytes": 65536,
    "EnforceStrictDacl": true
  },
  "EncryptionPolicies": {
    "DefaultSymmetricAlgorithm": "AES-256-GCM",
    "DefaultAsymmetricAlgorithm": "ECDSA-P384",
    "Pbkdf2Iterations": 64000,
    "EnforceMemoryPinning": true
  },
  "CertificatePolicies": {
    "EnforceCertificatePinning": true,
    "AllowSelfSignedCertificates": false,
    "PinnedPublicKeyHash": "39a0b1275498df31cb92b8d0034a718cdef9285038c1054a861cf86903bfca21",
    "BypassLocalTrustStore": true
  },
  "KeyPolicies": {
    "SymmetricKeyRotationIntervalSeconds": 3600,
    "SymmetricPacketThresholdLimit": 100000,
    "EntropySaltBase64": "SAYRA_Enterprise_Hardened_Salt_Vector_38294="
  },
  "AuditPolicies": {
    "EnableWindowsEventLogging": true,
    "AuditSeverityThreshold": "INFORMATION",
    "EnforceCryptographicAuditChain": true,
    "MaxLocalAuditEntries": 50000
  }
}
```

---

## 13 Storage

The client reserves a protected directory structure for all storage artifacts:

```
%ProgramData%\SAYRA_Client\
├── Data\
│   ├── client_config.json        # DPAPI-encrypted Workstation configuration
│   └── client_config.json.bak    # Cryptographically signed configuration backup
└── Databases\
    ├── offline_queue.db          # SQLCipher AES-256-CBC Queue Cache
    ├── telemetry_buffer.db       # SQLCipher AES-256-CBC Metric Cache
    └── security_audit.db         # SQLCipher cryptographically chained audit DB
```

### 13.1 Sizing & Retention Policies
*   **Storage Ceiling:** The total database directory sizing must not exceed **500 MB**.
*   **Log Retention:** Security logs are maintained locally for a maximum of **30 days** before being compressed, archived, and uploaded to the centralized logging server.
*   **Local Cleanup Loop:** A low-priority background thread executes daily to remove outdated logs, clear expired advertisement caches, and purge archived diagnostic data.
*   **Database Migration Engine:** Database schema updates must run within transactional blocks. SQLCipher key parameters must migrate seamlessly during schema updates without data loss.

---

## 14 APIs (Message & Signature Formats)

The client communicates over secure TCP sockets using highly structured, serialized JSON blocks.

### 14.1 Message Framing Specification
All transport frames use a binary protocol framing layout to prevent packet fragmentation issues:
```
+--------------------+---------------------+---------------------------------------+
| Magic Byte (0x53)  | Payload Length      | Cryptographic Payload JSON Block      |
| [1 Byte]           | [4 Bytes, Big Endian] [Variable Length]                      |
+--------------------+---------------------+---------------------------------------+
```

### 14.2 Encrypted Payload Format (JSON)
```json
{
  "EncryptedData": "Base64Encoded_AES_256_GCM_Ciphertext",
  "IV": "Base64Encoded_12Byte_Nonce",
  "AuthTag": "Base64Encoded_16Byte_Tag",
  "Timestamp": "2026-10-18T12:00:05Z",
  "SequenceNumber": 10294,
  "SenderIdentityHash": "SHA256_Hash_of_Workstation_ID"
}
```

### 14.3 Signature Format
Digital signatures appended to file assets and handshake frames conform to the following schema:
```json
{
  "SignatureAlgorithm": "ECDSA_P384_SHA384",
  "SignatureValue": "Base64Encoded_ECDSA_Signature_Bytes",
  "KeyFingerprint": "SHA256_Hash_of_Signing_Public_Key"
}
```

---

## 15 Testing Strategy

A production-ready security subsystem requires rigorous, automated verification across multiple failure vectors.

```
       [SECURITY TESTING PIPELINE]
┌───────────────────────────────────────┐
│              UNIT TESTS               │
│ - Validate DPAPI Envelopes            │
│ - Verify AES-GCM Encrypt/Decrypt      │
│ - Parse Config Validation Boundaries  │
└──────────────────┬────────────────────┘
                   v
┌───────────────────────────────────────┐
│           INTEGRATION TESTS           │
│ - Secure Named Pipe DACL Permissions  │
│ - Test Handshake & ECDH Key Exchange  │
│ - SQLCipher Database Open Sequences   │
└──────────────────┬────────────────────┘
                   v
┌───────────────────────────────────────┐
│             TAMPER TESTS              │
│ - Run Process Injection Mock Tools    │
│ - Simulate Registry Policy Overrides  │
│ - Verify Signature Match Drifts       │
└───────────────────────────────────────┘
```

### 15.1 Testing Classifications

#### 15.1.1 Unit Tests
*   `Verify_Dpapi_Encryption_Decryption_Succeeds_With_Valid_Entropy`
*   `Verify_Config_Parser_Throws_On_Malformed_Properties_Or_Value_Drifts`
*   `Verify_Hmac_Signature_Generator_Flags_Tampered_Message_Payloads`
*   *Validation Boundary Testing:* Asserts error states when values violate schema bounds or configuration parameters contain malformed JSON data.

#### 15.1.2 Penetration Tests
*   *Escalation Simulation:* Attempts to spawn command shells as a system level from interactive WPF environments, verifying immediate block responses.
*   *Asset Scraping:* Attempts to locate, decrypt, and parse local credentials databases without physical motherboard UUID access.

#### 15.1.3 Security Integration Tests
*   `Verify_Secure_Named_Pipe_Dacl_Blocks_Access_For_Standard_User_Token`
*   `Verify_SQLCipher_Database_Rejects_Connection_On_Incorrect_Hardware_Key`
*   `Verify_Socket_Handshake_Fails_On_Stale_Timestamp_Replay`
*   *Key Exchange Integration:* Simulates the complete Diffie-Hellman handshake across isolated network sockets to verify key establishment.

#### 15.1.4 Tamper Tests
*   *DLL Sideloading Mocks:* Verify that placing a rogue `bcrypt.dll` in the executable folder is caught by code integrity monitors.
*   *Hook Disconnection Tests:* Validate that forcefully removing the visual window's low-level keyboard hook triggers an automated session termination.
*   *Registry Alteration Interception:* Simulates unexpected registry changes to kiosk keys to verify immediate service reversion and alert dispatch.

#### 15.1.5 Replay Tests
*   *Monotonic Identifier Verification:* Attempts to replay previously accepted TCP packets containing identical sequence counters to verify automated packet drop routines.

#### 15.1.6 Stress Tests
*   *High-Frequency IPC Validation:* Spawns 1,000 threads to send overlapping encrypted requests over the secure Named Pipe to confirm that locking and memory models prevent deadlocks under Peak IO.

#### 15.1.7 Chaos Tests
*   *Interface Dropping:* Automatically drops local LAN interfaces during active cryptographic processes, verifying transaction states fail cleanly with zero data leakages.

#### 15.1.8 Recovery Tests
*   *Time Tampering Simulations:* Force changes to the system clock while active sessions are running, verifying the client calculates session timing using monotonic hardware clocks.
*   *Corrupted DB Reconstitution:* Simulates hard drive power interruptions during database transaction states, asserting database self-healing and quarantine routines.

---

## 16 Acceptance Criteria

Every subsystem must meet measurable, non-negotiable security bounds before deployment.

### 16.1 Measurable Security Metrics
1.  **DACL Verification:** The Named Pipe security descriptor must return access-denied for any user account outside of `LocalSystem` and the current active interactive gamer SID.
2.  **Encryption Validation:** Memory scanning tools (such as Process Hacker or WinDbg) running against the active process must return zero results when searching for the plaintext database key material or server password blocks.
3.  **Kiosk Escape Resilience:** Automated penetration tools testing keystroke injection (simulating standard Windows shortcuts) must return a **100% block rate**.
4.  **Signature Compliance:** The configuration verifier must reject configuration updates containing signature errors or expired certificate paths within **less than 100 ms**.
5.  **SQLCipher Hardening:** The local database file must appear as randomized binary data when viewed under disk analysis editors. Any manual file byte modification must render the database unreadable, triggering self-healing recovery pipelines.

---

## 17 Risk Analysis

### 17.1 Security Risks & Mitigations
*   **Risk:** Rogue Antivirus software flags low-level global keyboard hooks as malware.
*   **Mitigation:** The installation package registers binaries to system antivirus exemption channels, utilizing custom secure desktops as a primary defense if hooks are intercepted.

### 17.2 Operational Risks & Mitigations
*   **Risk:** Sudden hard power cut corrupts the active SQLCipher database files.
*   **Mitigation:** Configure SQLite in Write-Ahead Logging (WAL) mode with strict synchronous writes, storing redundant transaction states in backup configuration matrices.

### 17.3 Deployment Risks & Mitigations
*   **Risk:** Workstations with old hardware fail to process elliptic curve cryptography rapidly.
*   **Mitigation:** Implement platform-specific optimized assembly blocks using CNG wrappers to maximize performance on legacy systems.

### 17.4 Maintenance Risks & Mitigations
*   **Risk:** Staff rotation leads to lost administrative master bypass passwords or recovery configurations, causing complete workstation locking lockout.
*   **Mitigation:** Enforce centralized configuration-managed key escrows. Administrators can securely recover emergency bypass credentials by proving identity ownership to the master cloud server.

### 17.5 Compliance Risks & Mitigations
*   **Risk:** Changes to digital signature regulations or platform policies flag custom secure desktop switcher interops as anomalous OS activities.
*   **Mitigation:** Utilize standard standard Microsoft Authenticode code-signing certificate processes, registering the software formally with WHQL (Windows Hardware Quality Labs) and keeping alignment with modern Windows Security frameworks.

---

## 18 Future Integration

The Phase 3 Security Hardening subsystem establishes a secure baseline that integrates cleanly with subsequent development phases:

```
+-----------------------------------------------------------------------------------------+
|                                FUTURE INTEGRATION ROADMAP                               |
+-----------------------------------------------------------------------------------------+
│                                                                                         │
│  - Phase 4: Kiosk Mode & Lockdown                                                       │
│    -> Leverages IKioskSecurityService secure desktop hooks and Explorer block limits.   │
│                                                                                         │
│  - Phase 5: Virtual Disk & Game Patching                                                │
│    -> Integrates with IIntegrityValidator to verify file structures during patches.    │
│                                                                                         │
│  - Phase 6: Local Billing Integration                                                   │
│    -> Coordinates session counters directly using monotonic hardware clock baselines.   │
│                                                                                         │
│  - Phase 7: Peripheral & Hardware Guard                                                 │
│    -> Hooks into SCM and event log pipelines to track unauthorized USB insertions.       │
│                                                                                         │
│  - Phase 8: Remote Desktop Control                                                       │
│    -> Enforces ISecureIpcPolicyManager permission validation on desktop stream frames. │
+-----------------------------------------------------------------------------------------+
```

---

## 19 Implementation Checklist

### Epic 1: Ephemeral Key Exchange & Cryptographic Sockets (P0)
*   **Feature 1.1:** Elliptic Curve Diffie-Hellman Handshake Integration
    *   *Task:* Implement dynamic socket key exchange routines.
    *   *Subtask:* Write signature verification wrappers for pinned certificates.
*   **Feature 1.2:** AES-256-GCM Sockets Encryption
    *   *Task:* Develop socket message framing and payload encryption wrappers.
    *   *Subtask:* Create sequence number validators and timestamp validation filters.

### Epic 2: SQLCipher Database Hardening (P0)
*   **Feature 2.1:** SQLCipher Engine Configuration
    *   *Task:* Initialize database connections with AES-256-CBC parameters.
    *   *Subtask:* Implement PBKDF2 key derivation using machine DPAPI entropy.
*   **Feature 2.2:** Cryptographic Log Chain
    *   *Task:* Implement SHA-256 row-hashing chains inside the database audit logger.

### Epic 3: Secure Named Pipe IPC (P0)
*   **Feature 3.1:** Secure Pipe Creation & DACL Enforcement
    *   *Task:* Implement Win32 Security Descriptors on the Named Pipe.
    *   *Subtask:* Restrict access strictly to `LocalSystem` and active interactive user SIDs.
*   **Feature 3.2:** Caller Privilege Verification
    *   *Task:* Retrieve caller PID and perform token validation checks.

### Epic 4: Kiosk Shell Lockdown (P1)
*   **Feature 4.1:** Keyboard Hook Provider
    *   *Task:* Register a low-level global hook (`WH_KEYBOARD_LL`) to block OS hotkeys.
*   **Feature 4.2:** Custom Secure Desktops
    *   *Task:* Implement visual desktop switches using native `CreateDesktop` APIs.

---

## 20 Deliverables

The complete Phase 3 deployment bundle must deliver the following security artifacts:

### 20.1 Source Classes & Interfaces
*   `ICryptographyService.cs` (Cryptographic interface)
*   `IKioskSecurityService.cs` (Kiosk lockdown interface)
*   `IIntegrityValidator.cs` (Code integrity interface)
*   `ISecureIpcPolicyManager.cs` (IPC policy interface)
*   `CryptographyService.cs` (ProtectedData & AES-GCM implementation)
*   `KioskSecurityService.cs` (Win32 Keyboard hook & secure desktop switcher)
*   `IntegrityValidator.cs` (Authenticode & SHA-256 engine)
*   `SecureIpcPolicyManager.cs` (Named Pipe DACL manager)

### 20.2 Services & Background Workers
*   `SecurityAuditLogger` (Chained audit logger service)
*   `DebuggerWatchdogWorker` (Debugger and process injection watcher background worker)
*   `ConfigurationSyncService` (Signed configuration delta download worker)

### 20.3 Policies & Configuration Templates
*   `client_security_policy.json` (Security settings configuration profile)
*   `local_admin_template.json` (Bypass keys PBKDF2 hash baseline template)

### 20.4 Cryptographic Keys
*   `server_public.key` (Pinned public key certificate fingerprint)

### 20.5 Test Suite Portfolio
*   `Sayra.Client.Tests.Security/` (Automated suite containing over 45 unit, integration, and security tamper test definitions)
