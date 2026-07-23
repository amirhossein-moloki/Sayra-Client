# SAYRA Enterprise Windows Client: Phase 2 — Enterprise Client Platform
## Official Architectural Specification Document

**Author:** Principal Enterprise Systems Architect, Windows Platform Architect, and Technical Specification Writer
**Target Platform:** SAYRA Enterprise Windows Client (Phase 2)
**Status:** Approved & Released
**Classification:** Enterprise Confidential — Technical Reference Architecture

---

## 1 Executive Summary

### 1.1 Document Purpose
This document serves as the official Enterprise Architectural Specification and single source of truth for Phase 2 of the SAYRA Enterprise Windows Client system. It establishes a rigorous, comprehensive blueprint defining every required subsystem, interface, data flow, security control, and physical table structure.

The detail provided herein is engineered specifically to ensure that future human engineering teams and advanced AI implementation agents can develop, test, and deploy the Phase 2 system to production specifications with zero ambient ambiguity or need for architectural clarification.

### 1.2 Context & Mission
The SAYRA platform manages large-scale, high-density public gaming center workstations distributed across multi-branch physical environments. In such settings, network connections are unstable, workstation hardware is subjected to physical and virtual security attacks by technically skilled end-users, and operational reliability must remain at 99.99% (four nines) to prevent business revenue leakage.

### 1.3 Business & Enterprise Goals
*   **Operational Resilience:** Eliminate workstation session termination, billing discrepancies, or administrative lockout failures during local WAN or LAN connectivity drops.
*   **Zero Leakage Auditing:** Safeguard and record 100% of physical workstation events, accounting actions, and security alerts locally, guaranteeing upload upon reconnection.
*   **Decoupled Multi-Session Management:** Transition the product from a single monolithic thread model to a decoupled, multi-session, security-isolated enterprise client architecture.
*   **Centralized Operations (Hot Config):** Provide automated, validated, and cryptographically verified configuration push/pull capabilities to manage fleet settings without reboots.

### 1.4 Technical Goals
*   **Clean Architecture Enforcement:** Define precise boundaries dividing the Presentation, Application, Domain, and Infrastructure layers.
*   **Session Separation (0/1 Isolation):** Explicitly isolate low-privilege interactive user operations (Session 1+) running the WPF Client Shell from elevated execution hooks (Session 0) managed by the Windows Service Host.
*   **Asynchronous & Thread-Safe Design:** Architect non-blocking background workers, thread-safe message queues, and lock-free/low-lock data pipelines using C# modern asynchronous constructs (`SemaphoreSlim`, `Channel<T>`).
*   **Security-First Cryptography:** Encrypt local storage using SQLCipher (AES-256) bound to physical motherboard hardware keys (via Windows DPAPI), enforce server payload digital signature checks using pre-cached public keys, and deploy runtime tamper detection monitors.

---

## 2 Phase Scope

### 2.1 Included in Phase 2
The Phase 2 scope spans the following five core pillars of the SAYRA Client platform:
1.  **Notification System:** A high-throughput, prioritizing, scheduling, multi-channel (WPF Overlay & Windows Toast) engine with dynamic localizations, deduplication, and local SQL history tracking.
2.  **Configuration Synchronization:** A secure synchronization service supporting delta updates, signature verification, schema validation, and automatic rolling backups/rollbacks.
3.  **Enterprise Event Logging:** A structured JSON logging infrastructure segregating security, audit, operational, and performance events with tracing metadata (Trace, Correlation, and Session IDs) backed by local rotation and compressed batch uploads.
4.  **Offline Queue:** A SQLCipher-encrypted, DPAPI-bound transactional local queue with priority delivery, exponential backoff retries, dead letter queue (DLQ) quarantines, and replay protection.
5.  **Windows Integration:** Advanced OS bindings for Windows Toast APIs, Session State tracking (WTS), Event Viewer logging, file-system and registry watchers, SCM recovery, and Task Scheduler bypass-defenses.

### 2.2 Excluded from Phase 2
*   **Game Patch Distribution (Phase 3):** Differential local peer-to-peer (P2P) binary distribution.
*   **Peripheral Control (Phase 4):** USB charging controls, hardware power limits, and custom serial port interfaces.
*   **Direct Payment Gateway Integrations (Phase 5):** Workstation-level payment card processing (all billing is calculated via session state).
*   **Advanced Anti-Cheat Kernel Drivers (Phase 6):** Development of ring-3/ring-0 driver hook blockers.

### 2.3 Dependencies on Previous Phases
*   **Phase 1 Foundation:** Relies on the .NET 8 Windows Service host structure, base TCP Client Manager, challenge-response AuthManager, and base `session_state.json` layout developed during Phase 1.

### 2.4 Dependencies on Future Phases
*   **Admin Console UI (Phase 3):** Requires administrative panels to register and dispatch configuration updates and alerts defined in Phase 2 schemas.

### 2.5 Success Criteria
1.  **Zero-Loss Data Pipeline:** No billing, audit, or security events are lost over a continuous 72-hour simulated network outage.
2.  **Zero-Impact Performance Profile:** Running high-frequency logging and background sync workers must consume $<0.5\%$ CPU and $<45\text{MB}$ RAM during high-end graphical gaming execution.
3.  **Instant Watchdog Recovery:** WPF UI crashes must be detected and fully recovered (restoring lock/unlock visual states) inside 500ms.
4.  **100% Signature Enforcement:** Any modified, spoofed, or unsigned configuration file or system-level notification payload must be intercepted and rejected, triggering a security lockout.

---

## 3 High-Level Architecture

### 3.1 Structural System Architecture

```
                                      ┌────────────────────────────────────────────────────────┐
                                      │                     SAYRA SERVER                       │
                                      └───────────────────────────┬────────────────────────────┘
                                                                  │
                                            Secure TLS 1.3 Socket │ (Encrypted Payload & Signature)
                                                                  ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ WINDOWS SERVICE HOST (Session 0 - NT AUTHORITY\SYSTEM)                                                                │
│                                                                                                                       │
│  ┌───────────────────────┐       ┌───────────────────────┐       ┌───────────────────────┐       ┌─────────────────┐  │
│  │  NotificationRouter   │       │ WorkstationSyncService│       │ EnterpriseLogger      │       │  OfflineQueue   │  │
│  │  - Parses & Decrypts  │       │  - Pull/Push Config   │       │  - EventBus           │       │  - SQLCipher    │  │
│  │  - Signs Packet Auth  │       │  - Schema & Signature │       │  - Batching Engine    │       │  - DPAPI keys   │  │
│  └──────────┬────────────┘       └───────────┬───────────┘       └───────────┬───────────┘       └────────┬────────┘  │
│             │                                │                               │                            ▲           │
│             │                                ▼                               ▼                            │           │
│             │                    ┌────────────────────────────────────────────────────────────────────────┴───────┐  │
│             │                    │                      INFRASTRUCTURE LAYERS                                     │  │
│             │                    │  - Local SQLite DBs    - Serilog Log Rotator   - Named Pipe DACL Security      │  │
│             │                    └────────────────────────────────────────────────────────────────────────────────┘  │
│             │                                                                                                         │
└─────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────┘
              │
              │ IPC Named Pipe Bridge (\\.\pipe\SayraClientIpcPipe)
              ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ INTERACTIVE WPF UI CLIENT (Session 1+ - Low Privilege User Context)                                                    │
│                                                                                                                       │
│  ┌──────────────────────────────────────────────────────┐       ┌──────────────────────────────────────────────────┐  │
│  │               NOTIFICATION DISPATCHER                │       │                  LOCALIZATION                    │  │
│  │  - WPF Overlay Render Engine (Topmost Window Hooks)   │       │  - Lang.en.xaml / Lang.fa.xaml (RTL Native)      │  │
│  │  - Thread-Safe Toast Queue & Animations              │       │  - Dynamic String Param Interpolation            │  │
│  └──────────────────────────────────────────────────────┘       └──────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Clean Architecture Layers

The SAYRA client enforces strict Clean Architecture layering to isolate business domain logic from infrastructure details.

#### 3.2.1 Core Domain Layer (`Sayra.Client.Domain`)
*   **Content:** Pure domain models, system entities, value objects, and domain exceptions.
*   **Rules:** Zero external dependencies. No reference to third-party databases, serializing libraries, or WPF UI assemblies.
*   **Key Models:** `Notification`, `ConfigurationProfile`, `EventLogEntry`, `QueueMessage`.

#### 3.2.2 Application Layer (`Sayra.Client.Application`)
*   **Content:** Core business use cases, orchestrating services, command/event interfaces, and internal event-driven messaging structures.
*   **Rules:** Depends strictly on the Domain Layer. Defines abstract interfaces (e.g., `IOfflineQueue`, `INotificationChannel`) implemented in Infrastructure.
*   **Key Services:** `NotificationManager`, `ConfigurationSyncCoordinator`, `DiagnosticCollector`.

#### 3.2.3 Infrastructure Layer (`Sayra.Client.Infrastructure`)
*   **Content:** Concrete implementations of application interfaces, database access mechanisms (SQLCipher), operating system bindings, file system file writers, and cryptographic wrappers.
*   **Rules:** Depends on Application and Domain layers. Direct contact with Windows Win32 APIs, WinRT SDK libraries, and external DLLs.
*   **Key Engines:** `SqlCipherQueueRepository`, `WindowsEventLogService`, `RegistryWatcher`.

#### 3.2.4 Presentation Layer (`Sayra.Client.UI`)
*   **Content:** WPF windows, MVVM ViewModels, value converters, asset styles, localized resource dictionaries (`Lang.xaml`), and interactive event-loop bindings.
*   **Rules:** Runs purely in user desktop session environments (Session 1+). Communicates back to the Session 0 service host strictly via the authenticated Named Pipe IPC channel.

### 3.3 System Lifecycles

#### 3.3.1 Service Lifecycle (Session 0)
1.  **Initialize:** SCM initiates service execution. `Main()` bootstraps the .NET Generic Host, registers Dependency Injection classes, and binds logging targets.
2.  **Startup:** Loads the local configuration cache. Evaluates cryptographic signatures. Initializes the `SqlCipherQueueRepository` and decrypts database engines using local DPAPI keys.
3.  **Run:** Starts background workers (`SyncSchedulerWorker`, `EventQueueBatchingWorker`, `SystemWatchdog`). Launches the Named Pipe IPC Server and listens for UI attachments.
4.  **Shutdown:** SCM requests a stop. The host cancels running tokens (`CancellationTokenSource`). The event queue flushes the remaining in-memory buffers to SQLite. SQLite active connections are systematically checkpointed and closed.

#### 3.3.2 UI Lifecycle (Session 1+)
1.  **Launch:** Spawned by the Session 0 Service Host inside the context of the active interactive user's session token.
2.  **Attach:** WPF Shell initializes, boots the UI event loop, and opens a bi-directional connection over the local Named Pipe to the Session 0 Host.
3.  **Run:** Renders visual overlays, monitors player focus, intercepts desktop keys (via low-level hooks), and renders incoming visual notifications.
4.  **Close:** Closes named pipes, releases graphics resources, and exits. If the exit is unauthorized, the Session 0 Watchdog immediately re-spawns it.

#### 3.3.3 Worker Lifecycle (Background Processes)
*   **Execution Strategy:** Encapsulated inside C# `BackgroundService` class instances.
*   **Loop Control:** Bound to `await Task.Delay(Interval, cancellationToken)` loops.
*   **State Machine Alignment:** Workers dynamically adjust their sleep cycles or pause execution entirely based on changes in the core `ClientStateManager` (e.g., switching to offline state pauses remote sync loops).

---

## 4 Phase Components

### 4.1 Notification System

#### 4.1.1 Purpose
The Notification System provides an asynchronous, multi-channel alert delivery platform. It must display immediate visual notifications to users while processing silent administrative command notifications in the background.

#### 4.1.2 Subcomponent Specifications
*   **Notification Router:** The initial processing point. It intercepts the incoming network message envelope, verifies the signature, and sorts it by delivery intent: user-facing, system-level, or silent background commands.
*   **Notification Manager:** Orchestrates the core pipeline. It is responsible for handling the priority queue, local retention policies, scheduling routines, deduplication checks, and delivery tracking.
*   **Dispatcher:** The user-facing render engine residing in the WPF UI layer. It dynamically constructs custom visual toasts, handles animation timelines (fade-in, slide-out, user actions), and manages dynamic screen space placement.
*   **Priority Queue:** A custom in-memory sorting queue. It processes incoming messages by priority (`CRITICAL`, `URGENT`, `NORMAL`, `SILENT`). Critical warnings automatically preempt and bypass normal alerts.
*   **Scheduler:** Handles notifications scheduled to fire at a future epoch timestamp. It stores future schedules inside the SQLite repository and triggers them at the precise UTC time using a high-precision timer worker.
*   **TTL Engine:** Evaluates the Time-To-Live (TTL) header of a notification before rendering. If the system clock indicates that the current UTC time exceeds `Timestamp + TTL`, the payload is instantly discarded as stale.
*   **Deduplication:** A sliding-window hash cache that checks the unique ID (`NotificationId`) of incoming events against the last 500 received notifications in the past hour to prevent double-rendering from network packet replays.
*   **Rate Limiter:** Tracks the density of visual alerts. If the system receives more than 3 visual notifications in a 10-second period, the rate limiter queues the excess alerts, except those flagged as `CRITICAL`.
*   **Notification History:** A local record stored in SQLite containing past alerts. Users can review these through the WPF Client Shell history log.
*   **Localization:** Intercepts dynamic template keys (e.g., `TXT_SESSION_EXPIRING`) and combines them with parameters (e.g., `["10"]`) to produce fully localized strings using the active language resource dictionary (`Lang.fa.xaml` / `Lang.en.xaml`).
*   **Windows Toast (`WindowsNotificationChannel`):** Native Windows Action Center integration. Utilizes the WinRT toast notification platform to display alerts if the WPF application is minimized.
*   **Overlay Notifications (`WpfNotificationOverlayWindow`):** Custom high-performance topmost visual overlays that render on top of modern borderless full-screen games.
*   **Silent Notifications:** Commands that trigger system events, configuration refreshes, or diagnostics without presenting a user-facing visual element.
*   **Acknowledgement & Retry:** Generates a signed read/received receipt when a notification is displayed or interacted with. Receipts are placed in the `OfflineQueue` for upload. If delivery fails due to IPC drops, the service retries using an exponential backoff sequence.
*   **Notification Repository:** Data access abstraction pointing to local SQLite DB storage.
*   **Notification Policies:** Defines mutable rules governing muting, sound cues, overlay positioning, and display durations.

#### 4.1.3 Interface Design
Implemented via `INotificationChannel` and `INotificationManager`. Bi-directional flow is established through local IPC contracts.

#### 4.1.4 Failure Handling
If the WPF UI thread hangs, preventing overlay rendering, the Session 0 Service Host intercepts the failure and falls back to using the Win32 `WTSSendMessage` API to output critical messages on the Windows desktop lockscreen.

#### 4.1.5 Security & Threading
Every visual notification command must be signed with the server's private key. The public key `server_public.key` is used to verify signatures, preventing malicious scripts from triggering fake billing or administrative notifications. All UI presentation loops are strictly routed to the main WPF thread using the `Dispatcher`.

---

## 4.2 Configuration Synchronization

#### 4.2.1 Purpose
Ensures that all client workstations run the latest central rules, hardware mappings, kiosk policies, and advertisement assets, maintaining local configuration consistency across the network.

#### 4.2.2 Subcomponent Specifications
*   **Configuration Service:** The core state manager of the sync pipeline. It orchestrates the pull sequences, coordinates merges, validates target files, and executes hot-reloading hooks across the system.
*   **Scheduler:** Runs a background loop checking for updates. Utilizes a randomized jittered polling frequency (e.g., 15 minutes $\pm$ 2 minutes) to prevent thousands of workstations from simultaneously overloading the server.
*   **Delta Sync:** Computes SHA-256 hashes of individual local configuration sections. The server returns only the differences (`SyncDelta`) if the sections don't match, preserving bandwidth.
*   **Full Sync:** Executed on initial registration, after a local database corruption, or when requested by an administrator. Downloads the entire configuration schema.
*   **Conflict Resolution & Merge Strategy:** Reconciles differences between server policies and local offsets. The deterministic merge sequence is: `Global Server Configuration` $\rightarrow$ `Workstation Group Configuration` $\rightarrow$ `Station Local Hardware Overrides`.
*   **Rollback Engine:** If applying a newly synchronized configuration causes a system module to crash or fail validation, the engine instantly restores the cached backup (`config.json.bak`) and transmits an error report.
*   **Validation & Schema Checking:** All synchronized JSON data is validated against a pre-compiled JSON Schema before compilation. Any invalid properties, data types, or out-of-bounds parameters abort the merge.
*   **Digital Signature Verification:** Configuration payloads are cryptographically signed. The client rejects any configuration file whose digital signature cannot be validated using `server_public.key`.
*   **Versioning:** Every configuration profile includes a monotonic version integer (`VersionCode`). The client rejects profiles with a version code lower than the currently loaded file.
*   **Backup & Recovery:** Prior to writing a new configuration, the current active configuration is backed up to `config.json.bak`. Writing uses a transaction-safe swap approach: write payload to `config.json.tmp`, verify integrity, and swap with `config.json`.
*   **Configuration Cache:** An in-memory, thread-safe representation of active configuration objects to support high-speed parameter lookups without disk operations.
*   **Configuration Repository:** Interfaces with the local JSON files on disk.

#### 4.2.3 Lifecycle & Threading
Synchronization runs entirely on background thread pools. Hot-reloaded properties are swapped using thread-safe pointers (`volatile` references), ensuring no active operations are blocked during a configuration update.

---

## 4.3 Enterprise Event Logging

#### 4.3.1 Purpose
Event Logging records all system actions, transactions, security alerts, and performance metrics to support centralized tracing, debugging, and security forensics.

#### 4.3.2 Subcomponent Specifications
*   **Structured Logging (Serilog):** Implements structured JSON logging (using Serilog) rather than plain strings. This ensures that every entry preserves parseable field keys.
*   **Event Dispatcher:** An in-memory mediator event bus. It collects events generated across the application and dispatches them to appropriate targets (local logs, remote buffers, or security monitors).
*   **Event Categories:**
    *   *Security Events:* Tracks low-level hooks, unauthorized files, anti-tampering alerts, and credential validation. *Priority: CRITICAL.*
    *   *Audit Events:* Captures business transactions, user session changes, and administrator overrides. *Priority: HIGH.*
    *   *Operational Events:* Records component startup details, network reconnects, and file transfers. *Priority: NORMAL.*
    *   *Telemetry Events:* Tracks CPU/GPU metrics, memory utilization, and local queue sizes. *Priority: LOW.*
*   **Correlation & Session Tracing:** Every log entry is embedded with metadata:
    *   `CorrelationId`: Tracks a transaction across the server, service, and UI boundaries.
    *   `SessionId`: Maps events to a specific user's login duration.
    *   `TraceId`: Represents the micro-operation thread context.
*   **Log Rotation & Compression:** Local files are capped at 10MB per file with a maximum of 5 files retained on disk. Rotated files are compressed using GZip.
*   **Batch Upload Service:** Logs are buffered in SQLite and transmitted in compressed batches of 100 entries (or flushed immediately on `CRITICAL` events) to minimize network traffic.
*   **Offline Buffer:** Redirects the batch uploader to write directly to the local encrypted database when the workstation is offline, ensuring zero log data loss during network outages.

---

## 4.4 Offline Queue

#### 4.4.1 Purpose
The Offline Queue ensures data resilience during extended network outages, securing and buffering critical transactions, security logs, and user events for guaranteed delivery when online.

#### 4.4.2 Subcomponent Specifications
*   **Encrypted SQLite Queue:** Built on SQLite utilizing SQLCipher for AES-256 database-level encryption. The encryption key is derived using a PBKDF2 hash of local hardware parameters encrypted via Windows DPAPI.
*   **Queue Repository:** Provides the transactional interface (`Enqueue`, `Dequeue`, `Acknowledge`) to the SQLite database.
*   **Retry Queue:** Reads events in chronological order to maintain FIFO guarantees. If connection is lost, it backs off using an exponential timing sequence:
    $$T_{\text{retry}} = 2^{\text{retry\_count}} \times \text{BaseInterval} + \text{Jitter}$$
*   **Dead Letter Queue (DLQ):** Quarantines payloads that repeatedly trigger server-side parsing errors (e.g., HTTP 400 Bad Request) to prevent them from permanently blocking the queue delivery pipeline.
*   **Deduplication:** Incorporates unique UUID headers per event. The server filters out duplicate events based on these UUIDs to handle network-level retries safely.
*   **Transaction Safety:** Executes database operations using ACID transactions. If a crash occurs during a write operation, SQLite's write-ahead log (WAL) recovers the file to its last consistent state.
*   **Recovery & Cleanup:** Monitors database file sizing. Enforces storage limits (e.g., maximum 500MB). If limits are exceeded, it deletes the oldest operational telemetry logs while preserving security and audit records.
*   **Replay Protection:** Includes monotonic sequence counters and rolling cryptographic nonces in outgoing packets to prevent replay attacks.

---

## 4.5 Windows Integration

#### 4.5.1 Purpose
Leverages native Windows APIs to enforce system security, capture OS-level events, and integrate the background service with the active desktop user session.

#### 4.5.2 Subcomponent Specifications
*   **Windows Notifications:** Uses `Windows.UI.Notifications` WinRT APIs to post system alerts to the native Windows Action Center.
*   **Windows Event Log:** Registers a custom event source (`SAYRA_Client`) in the `Application` hive. System errors, SCM recovery triggers, and security violations are mirrored here for OS-level tracking.
*   **Named Pipes (Secure IPC):** Establishes bi-directional communication between the Session 0 service and Session 1+ WPF Shell over local Named Pipes (`\\.\pipe\SayraClientIpcPipe`). Access control lists (DACLs) restrict pipe access to the `LocalSystem` account and the active user's security identifier (SID).
*   **Session Change Events:** Listens to session state notifications (`WTS_SESSION_LOCK`, `WTS_SESSION_UNLOCK`, `WTS_SESSION_LOGON`, `WTS_SESSION_LOGOFF`) via WTSRegisterSessionNotification hooks, triggering dynamic UI alignment in response.
*   **Registry Monitoring (`RegistryWatcher`):** Monitors key registry hives (e.g., `HKLM\Software\Microsoft\Windows NT\CurrentVersion\Winlogon`). If a user attempts to manually reset the Windows Shell back to `explorer.exe` during lockdown, the watcher instantly reverts the change and locks the screen.
*   **File System Watchers (`FileSystemWatcher`):** Monitors game installation directories and local client configuration folders to detect and prevent unauthorized tampering with executable files or local data files.
*   **Task Scheduler Fallback:** Registers a high-privilege scheduled task (`NT AUTHORITY\SYSTEM`) that triggers on boot and user logon. If the primary Windows Service is disabled, the scheduler automatically reinstalls and restarts the agent.
*   **Service Control Manager (SCM) Integration:** Registers recovery actions in the SCM, configuring the service to automatically restart after 1 minute on the first, second, and subsequent failures.
*   **Windows Restart Manager:** Registers the application handle to ensure local settings and user session states are saved during Windows Update restarts, restoring the session state upon reboot.
*   **Desktop Session Isolation:** Isolates execution boundaries. Session 0 manages elevated operations (Registry, Process spawning) while Session 1+ processes user-facing UI overlays, preventing privilege escalation vulnerabilities.
*   **ETW (Event Tracing for Windows) Monitor:** Enforces real-time system audit monitoring by attaching to ETW providers (e.g., Microsoft-Windows-Kernel-Process, Microsoft-Windows-TCPIP) to detect unauthorized process creations or raw socket hijacking patterns instantly on ring-3.
*   **Windows Power Events Handler:** Listens to System Power Status Change signals (`WM_POWERBROADCAST`, `PBT_APMSUSPEND`, `PBT_APMRESUME`). This handles graceful low-power transition buffers (saving active state to local db) and immediate reconnect state checks upon wake.

---

## 5 Domain Models

### 5.1 Notification Payload Model (`NotificationPayload`)
*   **Purpose:** Captures the domain data required to validate, route, scheduled, and render a notification.
*   **Fields:**
    *   `Id` (Guid, Required): Globally unique identifier.
    *   `Title` (String, Required): Header text of the notification.
    *   `Body` (String, Required): Message content.
    *   `Category` (Enum `NotificationCategory`, Required): `BILLING`, `SYSTEM`, `SOCIAL`, `ADMINISTRATIVE`.
    *   `Priority` (Enum `NotificationPriority`, Required): `SILENT`, `NORMAL`, `URGENT`, `CRITICAL`.
    *   `TtlSeconds` (Int, Required): Lifetime duration. Set to `0` for infinite.
    *   `ActionCallbackToken` (String, Optional): Token used to execute client action callbacks.
    *   `LanguageToken` (String, Required): Dynamic translation identifier.
    *   `TemplateArgs` (Dictionary<String, String>, Required): Local replacement values.
    *   `CreatedAt` (DateTime, Required): UTC generation timestamp.
    *   `Signature` (String, Required): HMAC-SHA256 signature generated by the server.
*   **Validation:**
    *   `Id` must not be default/empty.
    *   `TtlSeconds` must be greater than or equal to `0`.
    *   `CreatedAt` must be within a 10-minute threshold of current UTC (unless a scheduled notification).
    *   `Signature` must be a valid 64-character hexadecimal string.
*   **Lifecycle:** `Created (Server)` $\rightarrow$ `Received & Verified (Router)` $\rightarrow$ `Queued (Priority Queue)` $\rightarrow$ `Rendered (WPF Overlay)` $\rightarrow$ `Acknowledged (Offline Queue)` $\rightarrow$ `Archived/Removed (Retention)`.

```json
{
  "Id": "d3b07384-d113-44f6-a83a-491295fc6e02",
  "Title": "Session Terminating",
  "Body": "Your session will expire in 10 minutes. Please extend your session.",
  "Category": "BILLING",
  "Priority": "CRITICAL",
  "TtlSeconds": 600,
  "ActionCallbackToken": "EXTEND_SESSION_1H",
  "LanguageToken": "TXT_SESSION_EXPIRING",
  "TemplateArgs": {
    "MinutesRemaining": "10"
  },
  "CreatedAt": "2024-10-25T14:32:00Z",
  "Signature": "a35f7cde9a31b4528c..."
}
```

---

### 5.2 Configuration Profile Model (`ConfigurationProfile`)
*   **Purpose:** The central data structure governing workstation operational boundaries.
*   **Fields:**
    *   `VersionCode` (Long, Required): Monotonically increasing version index.
    *   `WorkstationId` (String, Required): Target computer identifier.
    *   "NotificationSettings" (Object): System-level notification limits.
    *   "SyncSettings" (Object): Synchronization polling interval parameters.
    *   "LoggingSettings" (Object): File rotation and batch size configurations.
    *   "QueueSettings" (Object): Database sizes and retry timeouts.
    *   "ClientPolicies" (Object): Security locks, allowed processes, and UI options.
    *   `DigitalSignature` (String, Required): SHA-256 signature calculated over the profile content using the server private key.
*   **Validation:**
    *   `VersionCode` must be greater than the active local version.
    *   `WorkstationId` must match the local workstation identity or use wildcards (e.g., `*`).
    *   All numeric settings (e.g., `MaxDatabaseSizeMb`) must reside within designated threshold values.
*   **Lifecycle:** `Generated (Server)` $\rightarrow$ `Downloaded (Sync Service)` $\rightarrow$ `Validated (Schema/Signature Verifier)` $\rightarrow$ `Saved to config.json.tmp` $\rightarrow$ `Swapped with config.json` $\rightarrow$ `In-Memory Update` $\rightarrow$ `Active`.

```json
{
  "VersionCode": 10045,
  "WorkstationId": "STATION_042",
  "NotificationSettings": {
    "RateLimitMaxPerTenSeconds": 3,
    "HistoryRetentionLimit": 100,
    "EnableWindowsToastChannel": true
  },
  "SyncSettings": {
    "JitterIntervalMinutes": 15,
    "MaxRetryAttempts": 3,
    "EnableDeltaSync": true
  },
  "LoggingSettings": {
    "LogRotationLimitMb": 10,
    "RetainedFilesLimit": 5,
    "BatchSizeLimit": 100
  },
  "QueueSettings": {
    "MaxDatabaseSizeMb": 500,
    "ExponentialBackoffBaseSeconds": 3,
    "MaxBackoffSeconds": 300
  },
  "ClientPolicies": {
    "KioskLockdownActive": true,
    "AllowedUsbMassStorage": false
  },
  "DigitalSignature": "8f3b2a5c..."
}
```

---

### 5.3 Event Log Entry Model (`EventLogEntry`)
*   **Purpose:** Maps structured telemetry, audit, and security records.
*   **Fields:**
    *   `EventId` (Guid, Required): Globally unique identifier.
    *   `CorrelationId` (String, Required): Tracing ID across the network.
    *   `SessionId` (String, Optional): Active user session tracker.
    *   `TraceId` (String, Required): Specific task reference.
    *   `Category` (String, Required): `SECURITY`, `AUDIT`, `OPERATIONAL`, `PERFORMANCE`.
    *   `Severity` (String, Required): `DEBUG`, `INFO`, `WARNING`, `ERROR`, `FATAL`.
    *   `MessageTemplate` (String, Required): Structured message baseline.
    *   `PayloadFields` (Dictionary<String, Object>, Required): Extracted parameters.
    *   `Timestamp` (DateTime, Required): UTC log event timestamp.
*   **Validation:** `EventId` and `CorrelationId` must not be null or default. `Timestamp` must match client system clock.

```json
{
  "EventId": "9a751db5-12cf-4b77-a5eb-0676451e0892",
  "CorrelationId": "CORR-7781-A2",
  "SessionId": "SESS-USER-402",
  "TraceId": "0HLVBS7U...",
  "Category": "SECURITY",
  "Severity": "FATAL",
  "MessageTemplate": "Unauthorized registry modification intercepted on hive {RegistryHive}",
  "PayloadFields": {
    "RegistryHive": "HKLM\\Software\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon",
    "InterceptedValue": "explorer.exe",
    "ExecutingProcess": "malicious_script.exe"
  },
  "Timestamp": "2024-10-25T14:32:05Z"
}
```

---

### 5.4 Queue Item Model (`QueueItem`)
*   **Purpose:** Persistent wrapper utilized to safely buffer events inside the SQLCipher SQLite database.
*   **Fields:**
    *   `Id` (String, Required): Globally unique ID.
    *   `Priority` (Int, Required): Numeric sequence. Higher values jump delivery.
    *   `Category` (String, Required): Event categorization.
    *   `PayloadJson` (String, Required): Fully serialized dynamic JSON data.
    *   `RetryCount` (Int, Required): Delivery attempt tracker.
    *   `CreatedAt` (DateTime, Required): UTC storage timestamp.
    *   `SignatureHash` (String, Required): Local cryptographic check hash calculated using DPAPI keys to detect file tamper modifications.
*   **Validation:** `PayloadJson` must be valid, parseable JSON. `RetryCount` must be greater than or equal to `0`.

```json
{
  "Id": "QITEM-88910",
  "Priority": 10,
  "Category": "SECURITY_ALERT",
  "PayloadJson": "{\"EventId\":\"9a751db5...\"}",
  "RetryCount": 0,
  "CreatedAt": "2024-10-25T14:32:05Z",
  "SignatureHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
}
```

---

## 6 Interfaces

### 6.1 `INotificationChannel`
*   **Responsibilities:** Abstracts the target notification rendering pipeline. Allows dynamic configuration of visual outlets (WPF overlay vs Windows native Toasts).
*   **Expected Implementations:** `WpfNotificationChannel`, `WindowsNotificationChannel`.
*   **Lifetime:** Transient or Singleton registered in the Service Provider.
*   **Threading Model:** Fully asynchronous. Interface operations return `Task` and execute over background threads. Visual implementations marshaling to UI components must dispatch changes to the WPF Dispatcher.

```csharp
namespace Sayra.Client.Shared.Interfaces
{
    public interface INotificationChannel
    {
        NotificationChannelType ChannelType { get; }
        Task DeliverAsync(NotificationPayload payload, CancellationToken cancellationToken);
        Task DismissAsync(string notificationId, CancellationToken cancellationToken);
    }
}
```

---

### 6.2 `IWorkstationSyncService`
*   **Responsibilities:** Orchestrates pulled and pushed workstation configuration updates. Handles integrity verification and backup tasks.
*   **Expected Implementations:** `WorkstationSyncService` (running inside Session 0 Service Host).
*   **Lifetime:** Singleton. Holds references to active file streams and system managers.
*   **Threading Model:** Thread-safe execution using internal locks (`SemaphoreSlim`) to ensure that only a single synchronization event executes at any point.

```csharp
namespace Sayra.Client.Shared.Interfaces
{
    public interface IWorkstationSyncService
    {
        Task<SyncResult> ExecuteSyncAsync(SyncTriggerType triggerType, CancellationToken cancellationToken);
        Task<bool> ApplyConfigDeltaAsync(string sectionName, string jsonDelta, CancellationToken cancellationToken);
        Task<bool> RollbackToLastSafeConfigAsync(CancellationToken cancellationToken);
    }
}
```

---

### 6.3 `IOfflineQueue`
*   **Responsibilities:** Provides the transactional boundary for buffering outgoing operational telemetry, logs, and billing receipts locally during offline state.
*   **Expected Implementations:** `SqlCipherOfflineQueue` (backed by an encrypted SQLite DB).
*   **Lifetime:** Singleton.
*   **Threading Model:** Thread-safe by design. Thread-safety is enforced using thread locks and transactional query isolations to allow concurrent readers and writers without data locks.

```csharp
namespace Sayra.Client.Shared.Interfaces
{
    public interface IOfflineQueue
    {
        Task EnqueueAsync(QueueItem item, CancellationToken cancellationToken);
        Task<List<QueueItem>> DequeueBatchAsync(int batchSize, CancellationToken cancellationToken);
        Task AcknowledgeBatchAsync(List<string> itemIds, CancellationToken cancellationToken);
        Task QuarantineCorruptedItemsAsync(CancellationToken cancellationToken);
    }
}
```

---

## 7 Data Flow

```
+──────────────────────────────────────────────────────────────────────────────────────────+
|                                    STARTUP DATA FLOW                                     |
+──────────────────────────────────────────────────────────────────────────────────────────+

[Service Boot] ──► [Load config.json] ──► [Verify Digital Signature]
                                                    │
                 ┌──────────────────────────────────┴─────────────────────────────────┐
                 ▼ (Valid Signature)                                                 ▼ (Invalid / Corrupted)
     [Decrypt SqlCipher DB using DPAPI]                              [Restore config.json.bak Backup]
                 │                                                                    │
                 ▼                                                                    ▼
     [Launch UI Shell (Session 1+)]                                  [Validate & Start in Safe Lock Mode]
                 │
                 ▼
     [Initiate TCP Socket Conn to Server] ──► [Run challenge-response Handshake]
                                                               │
                                                               ▼
                                                     [Workstation Ready]
```

### 7.1 Notification Flow
1.  **Ingress:** Socket reader receives binary notification package. Decrypts envelope using local `SessionKey`.
2.  **Routing:** `NotificationRouter` verifies signature against `server_public.key`. Non-matching envelopes are rejected, and security alerts are generated.
3.  **Prioritization:** Manager parses Priority. `CRITICAL` alerts bypass the queue and rate limit checks. `NORMAL` and `SILENT` alerts are pushed into the local SQLite database.
4.  **IPC Dispatch:** Service Host writes message to the Named Pipe.
5.  **Visual Presentation:** WPF client reads pipe, resolves localized translation keys from `Lang.fa.xaml`, and renders the visual toast topmost.
6.  **Receipt Handling:** User actions (e.g., clicking "Acknowledge" or alert timeout) generate a signed delivery receipt, written to the `OfflineQueue` for upload.

### 7.2 Configuration Sync Flow
1.  **Trigger:** `SyncSchedulerWorker` triggers sync.
2.  **Handshake:** Workstation contacts server via `POST /sync/check`, sending current configuration version code and hashes.
3.  **Delta Processing:** If server detects profile delta, it returns `SyncDelta`. Otherwise, the sync process ends.
4.  **Signature Verification:** Configuration payload signature is validated against the server public key.
5.  **Staging:** Payload is validated against JSON schema and written to `config.json.tmp`.
6.  **Backup & Swap:** Current `config.json` is renamed to `config.json.bak`. `config.json.tmp` is renamed to `config.json`.
7.  **Hot Reload:** Memory configuration parameters are refreshed. System modules receive update signals and adjust settings in real-time.

### 7.3 Logging Flow
1.  **Capture:** System generates event (e.g., `UserSessionStarted`).
2.  **Resolution:** `EventDispatcher` injects metadata (Correlation ID, Session ID, Timestamp).
3.  **Local Buffering:** Serilog writes a structured JSON entry to local rotating log files on disk, and writes audit/security logs directly to SQLite.
4.  **Batch Dispatching:** `EventQueueBatchingWorker` queries logs, serializes them, applies GZip compression, and uploads batches of 100 entries via the TCP socket.
5.  **Acknowledgement:** Server returns upload confirmation. Client purges the processed records from local storage.

### 7.4 Offline Queue Flow
1.  **Disconnect:** TCP connection is lost. State machine shifts to `OFFLINE_GRACE_PERIOD`.
2.  **Buffering:** All logging and session activities are redirected to the encrypted SQLite database.
3.  **Deduplication:** Dynamic signatures and incremental sequence keys prevent duplicate event logging.
4.  **Reconnection:** Client reconnects and authenticates with the server.
5.  **Sequential Drain:** Dequeue service processes buffered items sequentially (respecting priority queue rules), uploading them in batches.
6.  **Purge:** Items are deleted from SQLite upon receiving server acknowledgement.

---

## 8 Threading Model

### 8.1 Background Workers Architecture
Background operations (e.g., polling, log batching, database cleanups) run strictly on background thread pool threads. This is implemented by inheriting from .NET's `BackgroundService` base class.

### 8.2 UI Thread Isolation
The WPF UI thread must remain responsive. All computational, cryptographic, or disk-bound tasks must execute on background threads before results are dispatched to the UI thread using WPF `Dispatcher.InvokeAsync` calls.

### 8.3 Synchronization & Concurrency
*   **State Locking:** Use `SemaphoreSlim(1, 1)` rather than `lock` statements to manage thread access to shared resources without blocking execution pools.
*   **Thread-Safe Collections:** Shared in-memory caches must utilize classes from the `System.Collections.Concurrent` namespace (e.g., `ConcurrentDictionary<TKey, TValue>`).
*   **System Channels (`Channel<T>`):** The local in-memory event pipeline uses `System.Threading.Channels` to manage producer-consumer patterns between modules with minimal memory allocations.

### 8.4 Deadlock Prevention Rules
1.  **No Blocking Calls:** Do not use `.Result` or `.Wait()` on asynchronous Task operations. Use `await` consistently.
2.  **Use ConfigureAwait:** For non-UI tasks, use `.ConfigureAwait(false)` to avoid capturing the UI synchronization context unnecessarily.
3.  **Strict Token Propagation:** Pass `CancellationToken` objects to all async methods to support graceful cancellation and prevent orphaned threads.

---

## 9 Reliability

### 9.1 Network Reconnect & Resilience
The TCP Client incorporates exponential backoff retries with added random jitter when reconnection attempts fail:
$$T_{\text{reconnect}} = 2^{\text{attempt\_count}} \times \text{BaseInterval} + \text{Jitter}$$
The maximum retry interval is capped at 5 minutes to prevent network floods.

### 9.2 Local Storage & Crash Recovery
The client operates transactionally. Key states are preserved using atomic swap operations, ensuring that database locks or unexpected power failures do not leave configuration or queue files in a corrupted state.

### 9.3 Database Corruption Mitigation
If a local SQLite database becomes corrupted, the system implements a structured fallback sequence:
1.  Quarantines the corrupted file (`offline_queue.db.corrupted`).
2.  Instantiates a clean database schema and logs a high-severity alarm.
3.  Launches a low-priority background thread to attempt recovery of salvageable data blocks from the quarantined database.

---

## 10 Security

```
+──────────────────────────────────────────────────────────────────────────────────────────+
|                                  SECURITY HOOK ARCHITECTURE                              |
+──────────────────────────────────────────────────────────────────────────────────────────+

            [Inbound Server JSON Packet] ──► [Verify RSA Digital Signature]
                                                        │
                                                        ▼ (Validated)
                             [Decrypt AES-256 Command Payload using SessionKey]
                                                        │
                                                        ▼
                                     [Execute Elevated System Commands]
```

### 10.1 Local Cryptographic Vault (DPAPI + SQLCipher)
The local SQLite database utilizes SQLCipher for database-level AES-256 encryption. The encryption password is dynamically derived using a machine-specific key encrypted via Windows DPAPI, preventing decryption if the database file is copied to another physical workstation.

### 10.2 Communication Integrity (TLS 1.3 & Digital Signatures)
All client-server communications use TLS 1.3 with strict Certificate Pinning. Configuration profiles and system command payloads require RSA digital signatures verified against a pre-cached server public key (`server_public.key`), preventing unauthorized modifications via man-in-the-middle (MITM) attacks.

### 10.3 Replay Protection
All incoming packets must contain a monotonic sequence number and a rolling cryptographic nonce. Payloads with timestamps outside a UTC window (e.g., $\pm 10$ seconds) are rejected.

---

## 11 Performance

### 11.1 Target Metrics
*   **Memory Footprint:** Keep UI Client RAM below 45MB.
*   **CPU Utilization:** Keep average background CPU usage below 0.5% (spikes below 2%).
*   **Compression Profile:** Use Brotli compression (level 5) on all log and telemetry batch uploads, reducing WAN bandwidth utilization.
*   **Local DB Limits:** Cap SQLite databases at 500MB, auto-pruning historical data if the threshold is breached.

### 11.2 Optimized Async Allocations
1.  **Zero-Allocation Pooling:** Use `ArrayPool<byte>.Shared` to manage socket read/write buffers, minimizing garbage collection allocations.
2.  **Non-Blocking Loops:** Telemetry sampling runs at low priorities to avoid interference with the host's active gaming session.

---

## 12 Configuration Hierarchy

Local configuration parameters are managed using a structured precedence model:
1.  **Server Overrides:** Global and Group-level configuration profiles pushed from the administration server.
2.  **Local Overrides:** Machine-specific settings (such as driver configurations or monitor refresh rates) defined in `config.json`.
3.  **Default Settings:** Built-in default values defined inside the application binary.

If server-pushed configurations fail schema validation, the client automatically restores the backup file `config.json.bak` to ensure continuous operation.

---

## 13 Storage Schemas

### 13.1 `Notifications` Table Schema
```sql
CREATE TABLE IF NOT EXISTS Notifications (
    Id TEXT PRIMARY KEY NOT NULL,
    Title TEXT NOT NULL,
    Body TEXT NOT NULL,
    Category TEXT NOT NULL,
    Priority TEXT NOT NULL,
    TtlSeconds INTEGER NOT NULL,
    ActionCallbackToken TEXT,
    LanguageToken TEXT NOT NULL,
    TemplateArgs TEXT NOT NULL, -- Serialized JSON dictionary
    CreatedAt TEXT NOT NULL,
    IsRead INTEGER DEFAULT 0,
    Signature TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IDX_Notifications_Priority_CreatedAt
ON Notifications(Priority, CreatedAt);
```

### 13.2 `OfflineQueue` Table Schema
```sql
CREATE TABLE IF NOT EXISTS OfflineQueue (
    Id TEXT PRIMARY KEY NOT NULL,
    Priority INTEGER NOT NULL,
    Category TEXT NOT NULL,
    PayloadJson TEXT NOT NULL,
    RetryCount INTEGER DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    SignatureHash TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IDX_OfflineQueue_Priority_CreatedAt
ON OfflineQueue(Priority, CreatedAt);
```

### 13.3 Data Retention Policies
Audit and security logs must be retained on the local disk for a minimum of 7 days if the workstation remains offline. Operational telemetry logs are capped at 48 hours and are automatically deleted during disk pressure events to free space.

---

## 14 APIs & Serialization Formats

### 14.1 Configuration Delta Sync API (`POST /api/v1/workstations/{id}/sync/check`)
*   **Request JSON:**
```json
{
  "ActiveVersionCode": 10044,
  "ActiveSectionHashes": {
    "NotificationSettings": "e3b0c442...",
    "ClientPolicies": "8f3b2a5c..."
  }
}
```
*   **Response JSON:**
```json
{
  "SyncRequired": true,
  "NewVersionCode": 10045,
  "Deltas": [
    {
      "TargetSection": "ClientPolicies",
      "SectionHash": "9a751db5...",
      "Base64DeltaPayload": "eyJLdW9za0xvY2tkb3duQWN0aXZlIjp0cnVlfQ==",
      "Signature": "e3b0c4..."
    }
  ]
}
```

### 14.2 Notification Broadcast TCP Packet (Code `0x301`)
*   **Frame Structure:**
```
[Header: 4 Bytes - Magic Code 0x53415952] [Message Code: 2 Bytes - 0x0301] [Payload Length: 4 Bytes] [Encrypted JSON Payload] [HMAC-SHA256 Signature: 32 Bytes]
```

---

## 15 Testing Strategy

### 15.1 Architectural Testing Matrices
*   **Unit Tests:** Validate priority queue behaviors, time-to-live calculations, schema validation engines, and HMAC signature parsing using pre-generated keys.
*   **Integration Tests:** Validate SQLite WAL recoveries, bi-directional IPC communications over Named Pipes with DACLs applied, and certificate pinning routines.
*   **Security Tests:** Simulate MITM attack vectors, unsigned configuration pushes, and database extraction attempts to verify cryptographic isolation.
*   **Chaos Tests:** Simulate sudden power failures, random packet loss, registry modifications, and database corruptions during transactional sync sequences.

---

## 16 Acceptance Criteria

### 16.1 Notification System
*   *Acceptance Metric:* Priority sorting must process and display `CRITICAL` warnings within 10ms of receipt, even if the primary queue is fully saturated with lower priority events.
*   *Acceptance Metric:* Verification checks must intercept and reject 100% of unsigned or improperly signed notification payloads.

### 16.2 Offline Queue
*   *Acceptance Metric:* Transactional integrity must be maintained. Zero data loss should occur across 10,000 simulated offline transaction writes during unexpected application terminations.

---

## 17 Risks & Architectural Mitigations

### 17.1 Security Bypass Attempts
*   *Risk:* Users with local administrative privileges may attempt to terminate the Session 0 background service.
*   *Mitigation:* The system registers a fallback recovery task in the Windows Task Scheduler configured to run with `NT AUTHORITY\SYSTEM` privileges, automatically restoring and restarting the primary agent if terminated.

### 17.2 DB Corruption via Hard Power Cuts
*   *Risk:* Workstations undergoing hard shutdowns may experience SQLite database corruption.
*   *Mitigation:* Employs Write-Ahead Logging (WAL) and synchronous database execution modes, ensuring the database can recover to a consistent state upon reboot.

---

## 18 Future Integration Plans

The Phase 2 architecture is designed to integrate cleanly with future phases:
*   **Phase 3 (Game Distribution):** The Configuration Synchronization service will act as the controller managing binary delta hashes for peer-to-peer patch distributions.
*   **Phase 4 (Peripheral Controls):** The Offline Queue will buffer and process physical peripheral usage events during network drops.

---

## 19 Implementation Checklist

### Epic 1 — Core Offline Resilience
- [ ] **Feature 1.1: Encrypted Storage Layer**
  - [ ] Implement SQLCipher DB bootstrap with PBKDF2 motherboard-bound keys.
  - [ ] Implement Windows DPAPI encryption wrappers for DB password protection.
  - [ ] Write integration test validating DB accessibility only with physical machine signature keys.
- [ ] **Feature 1.2: Transactional Queue Pipeline**
  - [ ] Build `SqlCipherOfflineQueue` repository with ACID transactional boundaries.
  - [ ] Implement exponential backoff schedules and Dead Letter Queue (DLQ) routines.
  - [ ] Write stress tests simulating sudden app termination during high-frequency writes.

### Epic 2 — Notification Subsystem
- [ ] **Feature 2.1: Router & Manager Core**
  - [ ] Implement `NotificationRouter` and RSA signature validation logic using `server_public.key`.
  - [ ] Build the in-memory `NotificationPriorityQueue` with custom aging sorting to prevent low-priority starvation.
- [ ] **Feature 2.2: WPF Overlay UI Integration**
  - [ ] Implement `WpfNotificationOverlayWindow` supporting multi-dpi topmost rendering.
  - [ ] Wire local named-pipe communication logic between Session 0 host and WPF shell.
  - [ ] Create Farsi and English resource dictionaries (`Lang.fa.xaml`, `Lang.en.xaml`) for runtime localization swaps.

### Epic 3 — Configuration Synchronization Subsystem
- [ ] **Feature 3.1: Polling Scheduler & Delta Sync**
  - [ ] Implement `SyncScheduler` utilizing randomized jitter schedules.
  - [ ] Build diff calculations engine returning JSON delta configurations (`SyncDelta`).
- [ ] **Feature 3.2: Validation, Backup & Rollback Engine**
  - [ ] Build JSON Schema validator mapping incoming configurations.
  - [ ] Implement transactional replacement: write to `.tmp` file, backup existing `.bak` file, and swap references.
  - [ ] Implement automated rollback to `config.json.bak` on initialization or load failures.

### Epic 4 — Enterprise Event Logging
- [ ] **Feature 4.1: Structured Serilog Logging & Dispatch**
  - [ ] Configure Serilog structured JSON rotation pipelines, restricting storage to 10MB x 5 files.
  - [ ] Build in-memory Mediator Event Bus with Correlation ID, Session ID, and Trace ID resolvers.
- [ ] **Feature 4.2: Batch Upload and Offline Buffering**
  - [ ] Implement `EventQueueBatchingWorker` processing logs in GZipped batches.
  - [ ] Integrate fallback offline redirection path routing logs to SQLite.

### Epic 5 — Windows Integration Services
- [ ] **Feature 5.1: Named Pipes Secure IPC**
  - [ ] Build local IPC pipe (`\\.\pipe\SayraClientIpcPipe`) applying SIDs DACL permissions restrictions.
- [ ] **Feature 5.2: Registry & FS Watchers**
  - [ ] Implement low-overhead `RegistryWatcher` monitoring critical system hives.
  - [ ] Implement `FileSystemWatcher` targeting active game library folders.
- [ ] **Feature 5.3: OS Event Listeners (ETW, WTS, Power)**
  - [ ] Build `WtsSessionChangeMonitor` using native Windows `WTSRegisterSessionNotification` APIs.
  - [ ] Implement kernel-process hook alerts monitoring ETW streams.
  - [ ] Implement `PowerStatusChangeHandler` intercepting `WM_POWERBROADCAST` Windows power states.

---

## 20 System Deliverables & Catalog

To fully implement Phase 2, the following physical files, binaries, configurations, and structures must be created:

### 20.1 Core Domain Entities (`Sayra.Client.Domain`)
*   `Notification.cs` (Domain model mapping notification variables)
*   `ConfigurationProfile.cs` (Value object detailing settings maps)
*   `EventLogEntry.cs` (Pure structured log model mapping tracing variables)
*   `QueueMessage.cs` (Database-safe queue structure mapping byte payloads)

### 20.2 Application Services & Contracts (`Sayra.Client.Application`)
*   `INotificationChannel.cs` (Interface mapping notification renderers)
*   `INotificationManager.cs` (Orchestrator interface for priority tracking)
*   `IWorkstationSyncService.cs` (Interface driving configuration updates)
*   `IOfflineQueue.cs` (Abstraction defining transactional storage pipelines)
*   `IEventDispatcher.cs` (Event mediator registration pipeline interface)
*   `NotificationManager.cs` (Orchestrates notification processing pipelines)
*   `ConfigurationSyncCoordinator.cs` (Drives delta comparison checks)
*   `LogBatchingManager.cs` (Assembles structured JSON batches for upload)

### 20.3 Infrastructure Components (`Sayra.Client.Infrastructure`)
*   `WpfNotificationChannel.cs` (Marshals payloads to topmost overlays)
*   `WindowsNotificationChannel.cs` (Integrates WinRT native Toast Action Center APIs)
*   `SqlCipherOfflineQueue.cs` (SQLite repository with DPAPI/SQLCipher encryption)
*   `SecureNamedPipeServer.cs` (Bi-directional pipe with custom SIDs DACLs)
*   `RegistryWatcher.cs` (Win32 low-overhead shell key modification interceptor)
*   `FileSystemWatcherService.cs` (Real-time file-system write protection watcher)
*   `WtsSessionChangeMonitor.cs` (WTSRegisterSessionNotification Windows hook binding)
*   `EtwMonitorService.cs` (ETW subscription monitoring handler)
*   `PowerStatusChangeHandler.cs` (System power listener intercepting WM_POWERBROADCAST)

### 20.4 Presentation Components (`Sayra.Client.UI`)
*   `WpfNotificationOverlayWindow.xaml` / `.xaml.cs` (Visual overlay canvas)
*   `WpfNotificationOverlayViewModel.cs` (Supports Two-Way reactive MVVM properties)
*   `Lang.fa.xaml` (RTL Persian dynamic translation resource dictionary)
*   `Lang.en.xaml` (LTR English dynamic translation resource dictionary)

### 20.5 Background Process Workers (`SayraClient`)
*   `SyncSchedulerWorker.cs` (BackgroundGenericHost task managing sync intervals)
*   `EventQueueBatchingWorker.cs` (Collects, GZips, and posts log batches)
*   `ClientWatchdogService.cs` (Session 0 service monitoring WPF process responsiveness)

### 20.6 Database Migrations & Schemas (`Storage/`)
*   `Migration_20241025_01_InitPhase2.sql` (Creates OfflineQueue, Notifications tables with Indexes)

### 20.7 Configuration Templates (`Configuration/`)
*   `config.json` (Local runtime JSON override configuration file)
*   `config.json.bak` (Safe rollback profile configuration file)

### 20.8 Test Suites (`Sayra.Client.Tests`)
*   `NotificationSystemTests.cs` (Validates priority queuing, rate limits, and TTLs)
*   `ConfigurationSyncTests.cs` (Validates schema mapping, signatures, and rollbacks)
*   `OfflineQueueTests.cs` (Validates transactional safety, SQLCipher security, and DLQs)
*   `WindowsIntegrationTests.cs` (Validates Named Pipe ACLs and WTS hooks)

---
*End of Specification Document.*
