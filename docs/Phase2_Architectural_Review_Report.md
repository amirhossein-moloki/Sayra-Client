# SAYRA ENTERPRISE WINDOWS CLIENT — PHASE 2 ARCHITECTURAL REVIEW & GAP ANALYSIS
**Auditor:** Principal Enterprise Systems Architect
**Target Architecture:** Windows Client (.NET 8 Background Service & WPF Shell)
**Scale Assumption:** Enterprise-scale gaming center management system (Thousands of clients across multi-center environments)

---

## EXECUTIVE SUMMARY
This document presents an exhaustive architectural review and gap analysis of **Phase 2** of the SAYRA Enterprise Windows Client system. Phase 2 targets the foundational communication, synchronization, diagnostic, and offline survival layers, specifically addressing:
1. **Notification System**
2. **Configuration Synchronization**
3. **Event Logging**
4. **Offline Queue**

As an enterprise-grade platform comparable to industry giants like GGLeap, Smartlaunch, SENET, and CyberCafePro, the SAYRA client must operate as a highly secure, reliable, high-performance, and resilient Windows Service (Session 0) integrated with a low-privilege interactive user interface (Session 1+). This document systematically decomposes each of these four domains, identifies critical architectural gaps, evaluates reliability/performance under scale, maps native Windows integration vectors, enforces cryptographic and communication security requirements, and provides an implementation readiness roadmap.

---

## PART 1 — NOTIFICATION SYSTEM REVIEW

An enterprise client operating in thousands of workstations must separate user-facing visual alerts from low-overhead system and administrative triggers. A robust notification engine must process, prioritize, schedule, route, and acknowledge notifications with minimal latency while remaining lightweight.

### 1.1 Structural Component Analysis

The following notification components are required to bridge the gap between Session 0 services and Session 1+ interactive user presentations:

```
[Server Notification Stream]
            │
            ▼ (Secure TLS Socket)
┌────────────────────────────────────────────────────────────────────────┐
│                        NOTIFICATION ROUTER                             │
│  - Decrypts and signs packet                                           │
│  - Parses destination (User vs System vs Silent)                       │
└──────────────────────────────────────────────────┬─────────────────────┘
                                                   │
                                                   ├───────────────────────────────┐
                                                   ▼                               ▼
                                       ┌───────────────────────┐       ┌───────────────────────┐
                                       │ USER-FACING EVENTS    │       │ SYSTEM-SILENT EVENTS  │
                                       └───────────┬───────────┘       └───────────┬───────────┘
                                                   │                               │
                                                   ▼                               ▼
┌────────────────────────────────────────────────────────────────────────┐       ┌───────────────────────┐
│                        NOTIFICATION MANAGER                            │       │   SYSTEM WORKER       │
│  - Priority Queue (Urgent/Medium/Low)                                  │       │   (Action Handler)    │
│  - Rate Limiter & Deduplicator                                         │       └───────────────────────┘
│  - Scheduler & Local History (SQLite)                                  │
└──────────────────────────────────────────────────┬─────────────────────┘
                                                   │
                                                   ▼ (IPC Named Pipe)
┌────────────────────────────────────────────────────────────────────────┐
│                        NOTIFICATION DISPATCHER                         │
│  - Localized WPF Toast Render Engine                                   │
│  - Dynamic Localization Parser (en/fa Dictionary)                      │
│  - Interactive Action Callback (Buttons, Shell Exec)                   │
└────────────────────────────────────────────────────────────────────────┘
```

#### 1. `NotificationManager` (Core Engine)
*   **Why It Is Needed:** Coordinates the notification lifecycle. It buffers incoming messages, evaluates their priority, applies scheduling offsets, checks for deduplication, and handles local retention.
*   **Responsibility:** Evaluates notification rules, manages the priority queues, stores history, and tracks acknowledgement states.
*   **Dependencies:** `ILocalStorage` (SQLite history), `IClock` (for scheduling and expiration checks).
*   **Failure Scenarios:** SQLite DB lockup under heavy write loads or storage corruption, preventing notification logging.
*   **Recovery Strategy:** Falls back to an in-memory queue and logs to the Windows Event Log or local fallback file, attempting to repair the SQLite DB on next lock state transition.

#### 2. `NotificationDispatcher` (Presentation Engine)
*   **Why It Is Needed:** Handles the visual rendering of alerts in Session 1+ user desktop space, managing screen position, animation timelines, and dynamic layouts (e.g. WPF overlay toasts).
*   **Responsibility:** Displays visual UI toasts, handles user interaction event capture, and triggers dismiss animations.
*   **Dependencies:** WPF Shell thread, `IThemeService`, `ILocalizationService`.
*   **Failure Scenarios:** WPF UI thread hang or freeze, preventing alerts from rendering.
*   **Recovery Strategy:** The background service detects UI unresponsiveness and logs system critical notifications using the Win32 `WTSSendMessage` API as an out-of-process Session 0 dialog fallback.

#### 3. `NotificationRouter` (Message Parser)
*   **Why It Is Needed:** Distributes notifications to appropriate subsystems. Not all notifications are user-facing; many are silent background commands or telemetry triggers.
*   **Responsibility:** Decodes incoming message types, validates signatures, and routes them to either user-facing presentation queues or internal system command handlers.
*   **Dependencies:** `ICryptoService`, `IInternalEventBus`.
*   **Failure Scenarios:** Invalid/malformed signature or message format causing decryption to crash.
*   **Recovery Strategy:** Discards the bad package, registers a security audit violation, and requests re-transmission of the specific notification ID from the server.

#### 4. Notification Channel Abstraction (`INotificationChannel`)
*   **Why It Is Needed:** Decouples notification delivery mediums. Allows sending notifications via multiple channels: WPF UI overlays, native Windows Action Center toasts, SMS overlays (for dynamic authentication tokens), or sound/voice synthesizer alarms.
*   **Responsibility:** Abstract interface exposing standard delivery contracts (`SendAsync`, `CancelAsync`).
*   **Dependencies:** Channel-specific implementations (e.g., `WpfNotificationChannel`, `WindowsNotificationChannel`).
*   **Failure Scenarios:** Specific channel API failure (e.g., native Windows toast registration blocks due to policy).
*   **Recovery Strategy:** Graceful fallback to the default standard WPF Overlay toast window.

#### 5. Notification History Engine
*   **Why It Is Needed:** Gamers often close notifications accidentally. A local historical record allows players or administrators to review past announcements, receipt receipts, and billing alerts.
*   **Responsibility:** Persists notifications to local storage with read/unread flags, timestamps, and categories.
*   **Dependencies:** SQLite Database, Local Storage Vault.
*   **Failure Scenarios:** Database disk full.
*   **Recovery Strategy:** Enforces a strict FIFO retention policy (retains max 100 historical items, deleting oldest).

#### 6. Notification Priority Queue
*   **Why It Is Needed:** Critical alerts (such as "Session ending in 1 minute!" or "Fire Alarm!") must pre-empt low-priority notifications (e.g., "Daily tournament starting soon").
*   **Responsibility:** Sorts notification delivery streams dynamically using an priority enum: `CRITICAL`, `URGENT`, `NORMAL`, `SILENT`.
*   **Dependencies:** In-memory priority sorting queue.
*   **Failure Scenarios:** Priority inversion or starvation of lower priority notifications.
*   **Recovery Strategy:** Employs an aging algorithm where low-priority notifications have their priority artificially incremented if they stay queued for more than 5 minutes.

#### 7. Silent Notifications (System Prompts)
*   **Why It Is Needed:** Triggers background diagnostic routines, executes policy enforcement, synchronizes localized configurations, or initiates quiet pre-caching without interrupting the player's gaming session.
*   **Responsibility:** Handles system-level triggers with zero UI representation.
*   **Dependencies:** Internal Command Dispatcher, Worker Engines.
*   **Failure Scenarios:** A silent notification attempts to invoke a UI thread component, resulting in a thread cross-access exception.
*   **Recovery Strategy:** Strictly validate that silent notifications are handled on background threads with zero UI dependencies.

#### 8. Native Windows Toast Integration (`WindowsNotificationChannel`)
*   **Why It Is Needed:** Displays warnings outside the game window when playing in windowed mode, or directly on the lock screen before a user session starts.
*   **Responsibility:** Uses Windows WinRT/ToastNotification API to display alerts.
*   **Dependencies:** Windows SDK contracts.
*   **Failure Scenarios:** Disabled Windows notification settings or restricted focus assist block the notification.
*   **Recovery Strategy:** Detect blocking states and fall back to the custom WPF overlay layer.

#### 9. Actionable Notifications (Dynamic Callbacks)
*   **Why It Is Needed:** Allows users to interact directly with notifications (e.g., clicking "Accept Matchmaking" or "Extend Session by 1 Hour").
*   **Responsibility:** Attaches dynamic commands to visual button clicks in the toast UI.
*   **Dependencies:** Command Dispatcher, IPC Bridge.
*   **Failure Scenarios:** The session expires before the user clicks, leaving an orphan action handler.
*   **Recovery Strategy:** Associate actions with strict expiration token TTLs; clicking an expired token triggers a silent "Action Expired" toast rather than execution.

#### 10. Persistent Notifications
*   **Why It Is Needed:** Alerts that require active user intervention (e.g. system warnings or critical billing extensions) must remain pinned on the screen and cannot be swiped away.
*   **Responsibility:** Pins UI overlays on top of all windows (including full-screen games if running in borderless mode).
*   **Dependencies:** WPF Window Styling (`Topmost="True"`), Win32 `SetWindowPos` overlay hooks.
*   **Failure Scenarios:** Full-screen exclusive games cover the persistent toast.
*   **Recovery Strategy:** Periodically force the visual toast handle to the top of the z-order index using Win32 API loops.

#### 11. Notification Expiration & TTL Engine
*   **Why It Is Needed:** Prevents stale alerts from rendering (e.g. showing "Happy Hour ends in 10 minutes" 5 hours after happy hour ended).
*   **Responsibility:** Checks timestamp + TTL parameters of incoming notifications, discarding expired payloads before delivery.
*   **Dependencies:** Client-Server Time Synchronizer.
*   **Failure Scenarios:** System clock manipulation renders TTL checks invalid.
*   **Recovery Strategy:** Uses monotonic ticks (`QueryPerformanceCounter`) or NTP-synchronized time for TTL computations.

#### 12. Notification Scheduling Service
*   **Why It Is Needed:** Handles deferred delivery (e.g. scheduling a warning toast 10 minutes before the prepaid session expires).
*   **Responsibility:** Schedules local notifications to trigger at future epoch timestamps.
*   **Dependencies:** Persistent Scheduler Database, Thread Pools.
*   **Failure Scenarios:** Station crashes/reboots before the scheduled notification fires.
*   **Recovery Strategy:** On startup, the scheduler scans database schedules, immediately firing past-due critical notifications and rescheduling future ones.

#### 13. Notification Deduplicator
*   **Why It Is Needed:** High network latency can cause retry mechanisms to send identical notifications multiple times, causing annoyance.
*   **Responsibility:** Hashes notification content and tracks IDs in a sliding window memory set (e.g., tracking the last 50 notification IDs received in the last hour) to discard duplicates.
*   **Dependencies:** Dynamic Unique ID generation contracts.
*   **Failure Scenarios:** Memory overhead if tracking too many unique hashes.
*   **Recovery Strategy:** Uses a Bloom Filter or strict sliding window cache capped at 500 items.

#### 14. Rate Limiter
*   **Why It Is Needed:** Prevents malicious processes or misconfigured server scripts from flooding the player's screen with alerts.
*   **Responsibility:** Restricts incoming notification delivery rates to a maximum threshold (e.g., max 3 visual toasts per 10 seconds).
*   **Dependencies:** Token Bucket Algorithm.
*   **Failure Scenarios:** Blocks critical notifications if a sudden flood occurs.
*   **Recovery Strategy:** Excludes `CRITICAL` priority notifications from rate-limiting policies.

#### 15. Notification Acknowledgement (Reliability Loop)
*   **Why It Is Needed:** Ensures critical regulatory or administrative alerts are actually read.
*   **Responsibility:** Transmits a signed receipt package back to the server containing: Client ID, Notification ID, User Action (Read/Dismissed/Expired), and Epoch Timestamp.
*   **Dependencies:** `IOfflineQueue`, TCP Client connection.
*   **Failure Scenarios:** Workstation goes offline before receipt dispatch.
*   **Recovery Strategy:** Logs the acknowledgment receipt to the `OfflineQueue` for guaranteed delivery when connectivity is restored.

#### 16. Retry Mechanism
*   **Why It Is Needed:** Handles packet drops during transit or sudden IPC disconnects.
*   **Responsibility:** Retries IPC transfer from Session 0 service to Session 1 WPF shell.
*   **Dependencies:** Named Pipe IPC Bridge.
*   **Failure Scenarios:** WPF UI process has crashed and is being rebooted by the Service Watchdog.
*   **Recovery Strategy:** Retries delivery with exponential backoff, holding the notification in a memory buffer until the UI process is fully re-established.

#### 17. Localization Engine (`Lang.en.xaml` / `Lang.fa.xaml`)
*   **Why It Is Needed:** Gaming cafes cater to diverse player bases, requiring dynamic locale switching.
*   **Responsibility:** Maps incoming tokenized templates (`KEY: SESSION_EXPIRING`) to appropriate language files.
*   **Dependencies:** Dynamic Localization Resources.
*   **Failure Scenarios:** Missing translation keys in resource dictionaries.
*   **Recovery Strategy:** Fall back to English or return the raw token string to prevent blank notifications.

#### 18. Notification Categories
*   **Why It Is Needed:** Allows players to filter and toggle alerts (e.g., muting tournament alerts while keeping billing alerts active).
*   **Responsibility:** Segregates notifications into logical groups: `BILLING`, `SYSTEM`, `SOCIAL`, `ADMINISTRATIVE`.
*   **Dependencies:** User Preferences Configuration.
*   **Failure Scenarios:** User mutes billing or critical security categories.
*   **Recovery Strategy:** Enforces hard-coded non-mutable channels for `BILLING` and `SYSTEM` groups.

#### 19. User vs System Notifications Separation
*   **Why It Is Needed:** Ensures that system command payloads (e.g. "reboot station", "download update") bypass the WPF UI display pipeline entirely and execute immediately in Session 0.
*   **Responsibility:** Categorizes execution contexts at the entry-level router.
*   **Dependencies:** `ClientAppLifetimeWorker`, Windows Service Host.
*   **Failure Scenarios:** Security breach where a user attempts to execute system-level commands via spoofed user alerts.
*   **Recovery Strategy:** Enforce absolute signature validation and privilege checks on system-level payloads.

#### 20. Notification Templates (Tokenized Payloads)
*   **Why It Is Needed:** Minimizes WAN network bandwidth. Instead of transmitting full visual styling and redundant copy texts, the server sends key-value parameters: `[ID: 101, Temp: "SESSION_END", Arg0: "15"]`.
*   **Responsibility:** Merges data arguments with localized templates.
*   **Dependencies:** Serialization Engine.
*   **Failure Scenarios:** Mismatched argument counts causing rendering exceptions.
*   **Recovery Strategy:** Wrap the template parser in defensive try-catch loops, falling back to a raw string output on parsing failure.

---

## PART 2 — CONFIGURATION SYNCHRONIZATION REVIEW

Managing thousands of client machines across distinct geographical regions requires a centralized remote configuration model. The client must systematically pull, push, validate, merge, and rollback configuration profiles under unstable network states.

### 2.1 Synchronization Flow Engine

```
[Remote Server Configuration Engine]
                │
                ├──────────────────────────────────────┐ (Push Notification via TCP)
                ▼ (Pull Request)                       │
┌──────────────────────────────────────────────────────┼─────────────────┐
│                       WORKSTATION SYNC SERVICE       │                 │
│  - Handshakes & checks local config version hash     │                 │
│  - Receives SyncDelta or Full Payload                ◄─┘                 │
│  - Executes Signature & Cryptographic Verification                     │
└───────────────────────┬────────────────────────────────────────────────┘
                        │
                        ▼ (Valid)
┌────────────────────────────────────────────────────────────────────────┐
│                        CONFIGURATION MANAGER                           │
│  - Compares and merges local changes (Merge Strategy)                  │
│  - Validates constraints (Schema, types, boundary ranges)             │
│  - Writes configuration to transaction safe storage (.tmp -> swap)     │
└───────────────────────┬────────────────────────────────────────────────┘
                        ├───────────────────────────────┐
                        ▼ (Success)                     ▼ (Validation Failure)
            ┌───────────────────────┐       ┌───────────────────────┐
            │   APPLY NEW PROFILE   │       │   ROLLBACK ENGINE     │
            │  - Notify modules     │       │  - Restore snapshot   │
            │  - Update local state │       │  - Log server warning │
            └───────────────────────┘       └───────────────────────┘
```

### 2.2 Missing Configuration Infrastructure Components

The current codebase contains placeholder sync components. To achieve production readiness, the following services must be implemented:

1.  **Sync Scheduler (`SyncScheduler`):** Governs dynamic polling and update sequences. Polling must use a jittered interval (e.g. 15 minutes $\pm$ 2 minutes) to prevent a thunderous herd effect where thousands of workstations request configuration checks from the server at the exact same second.
2.  **Pull vs. Push Coordinator:**
    *   *Push:* High priority configuration changes (e.g. emergency policy lockdowns or game blocking) trigger instant TCP websocket packets.
    *   *Pull:* The client uses scheduled pull loops to guarantee alignment if a TCP socket notification was dropped.
3.  **Delta Synchronization Engine:** To conserve bandwidth, the client computes local SHA-256 hashes of individual config sections (e.g., `HardwareConfig`, `KioskSettings`, `AdRotations`). The server reviews the hashes and transmits only the changed segments (`SyncDelta`).
4.  **Configuration Rollback Engine:** If applying a newly synchronized configuration causes a critical module to crash or fail validation (e.g., the kiosk service fails to lock), the rollback engine automatically restores the previously cached safe configuration (`config.json.bak`) and reports the defect to the cloud.
5.  **Cryptographic Integrity & Signature Verifier:** All synchronized payloads must be signed by the server's private key. The workstation verifies the signature using the pre-distributed public key (`server_public.key`) before merging, protecting against MITM packet injection.
6.  **Configuration Merge Strategy Coordinator:** Reconciles differences between server policies (global settings, network rules) and local workstation overrides (specific monitor refresh rates, driver settings, or localized diagnostic offsets) using a deterministic precedence hierarchy: `Global Server` $\rightarrow$ `Group Server` $\rightarrow$ `Station Local`.
7.  **Partial Update Execution Engine:** Allows modifying specific keys (e.g. changing a single advertisement image URL) without forcing a full configuration reload, avoiding transient system flickers or unnecessary module restarts.
8.  **Sync State Tracker & Metrics Engine:** Publishes synchronization performance metrics (e.g., latency, download sizes, merge durations, schema errors) to local telemetry for performance tracing.

---

## PART 3 — EVENT LOGGING REVIEW

Enterprise auditing requires standardizing events across multiple domains (security, auditing, operational health, and system performance) to enable rapid troubleshooting and security forensics.

### 3.1 Event Categories & Classifications

The client must maintain a high-performance in-memory Event Bus that categorizes and processes events with clean separation:

```
+─────────────────────────────────────────────────────────────────────────────────+
|                               SAYRA INTERNAL EVENT BUS                          |
+─────────────────────────────────────────────────────────────────────────────────+
        │                               │                               │
        ▼ (Audit Events)                ▼ (Security Events)             ▼ (Perf Events)
┌───────────────────────┐       ┌───────────────────────┐       ┌───────────────────────┐
│   AUDIT LOG ENGINE    │       │   SECURITY MONITOR    │       │   TELEMETRY ENGINE    │
│  - User transactions  │       │  - Tampering alerts   │       │  - CPU, VRAM, RAM     │
│  - Session transitions│       │  - Unauthorized USB   │       │  - Framerate, Latency │
└───────────┬───────────┘       └───────────┬───────────┘       └───────────┬───────────┘
            │                               │                               │
            └───────────────────────┬───────┴───────────────────────────────┘
                                    ▼
                        ┌───────────────────────┐
                        │ EVENT BATCH COMPRESSION│
                        │  - GZip Serialization │
                        │  - SQLite Buffering   │
                        └───────────┬───────────┘
                                    ▼
                        ┌───────────────────────┐
                        │   OFFLINE QUEUE       │
                        │  - Guaranteed Delivery│
                        └───────────────────────┘
```

1.  **Security Events:** Track unauthorized software executions, low-level keyboard hook blockages, configuration file changes, physical time manipulation, and debugger attachments. *Priority: CRITICAL.*
2.  **Audit Events:** Maintain a monotonic chain of business transactions, user logons, logoffs, payment completions, and administrative overrides. *Priority: HIGH.*
3.  **Operational Events:** Detail the operational flow of system modules: startup steps, network re-connections, IPC session creations, process lifecycles, and configuration swaps. *Priority: NORMAL.*
4.  **Performance Events:** Track system metrics, hardware thermal warnings, disk write latency spikes, network packet drop rates, and FPS measurements. *Priority: LOW.*

### 3.2 Distributed Tracing & Correlation Architecture
An administrative action triggered on the Admin Panel (e.g. remote game launch) can fail across several boundaries: the TCP socket, the background service, the IPC channel, or the WPF UI. To debug these distributed actions, every command MUST embed a telemetry trace context:
*   **Correlation ID:** Passed from the server. Follows the transaction across the entire execution boundary (Server $\rightarrow$ Service $\rightarrow$ WPF UI $\rightarrow$ Process Manager).
*   **Session ID:** Uniquely identifies the current active player session, allowing developers to isolate and filter logs to a specific user's login duration.
*   **Trace ID:** Maps specific multi-threaded internal operations (e.g., executing a local diagnostic query).

### 3.3 Log Management Pipelines

*   **Log Rotation:** Local diagnostic log files (using Serilog) must be constrained to prevent storage exhaustion. Files are rotated daily and capped at a maximum size (e.g., 10MB per file, retaining a maximum of 5 files before deletion).
*   **Structured Logging Format:** Logs must be serialized as structured JSON fields (not plain strings) to allow fast parsing by log ingestion engines (e.g. Elasticsearch, Grafana Loki).
*   **Log Serialization & Compression Engine:** Operational and performance logs are compressed using GZip/Brotli prior to transmission, preserving WAN bandwidth.
*   **Batch Upload Service:** Logs are not transmitted one-by-one. They are written to a localized SQLite buffer and uploaded in compressed batches of 50–100 records or flushed immediately on `CRITICAL` events.

---

## PART 4 — OFFLINE QUEUE REVIEW

Workstations operate in environments where local networks are frequently unstable or routers may drop connection under high load. The `OfflineQueue` is the primary mechanism of local resilience, ensuring zero transaction or audit event leakage during extended offline durations.

### 4.1 Queue Architecture & Data Integrity Flow

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                                   EVENT PRODUCER                                │
│                  - Generates transaction, audit, or security events             │
└────────────────────────────────────────┬────────────────────────────────────────┘
                                         ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                                 QUEUE MANAGER                                   │
│  - Allocates priority (CRITICAL > HIGH > NORMAL)                                │
│  - Formats unique ID and signs event cryptographic hash                         │
└────────────────────────────────────────┬────────────────────────────────────────┘
                                         ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              PERSISTENT SQLITE QUEUE                            │
│  - Encrypted database (SQLCipher / AES-256 with DPAPI keys)                     │
│  - GZip compressed serialization payloads                                      │
│  - Strict storage limits (e.g., FIFO capped at 500MB)                           │
└────────────────────────────────────────┬────────────────────────────────────────┘
                                         ├────────────────────────────────────────┐
                                         ▼ (Online)                               ▼ (Corrupted)
┌────────────────────────────────────────────────────────────────────────┐ ┌──────────────────────┐
│                              RETRY ENGINE                              │ │  CORRUPTION CLEANUP  │
│  - Reads chronological order (preserving FIFO queue guarantees)         │ │  - Quarantines DB    │
│  - Dynamic Exponential Backoff (3s -> 9s -> 27s -> 81s max)             │ │  - Re-creates DB     │
│  - Dead Letter Queue (DLQ) isolation after 5 failed transmission retries│ │  - Logs audit alert  │
└────────────────────────────────────────────────────────────────────────┘ └──────────────────────┘
```

### 4.2 Detailed Operational Specifications

*   **Queue Storage & Encryption:** The queue must be persisted in an encrypted local database (SQLite + SQLCipher or DPAPI encrypted sequential file). Plaintext logging of transactions or audit logs is prohibited.
*   **Prioritization Engine:** Events must be processed out-of-order when transmitting, allowing `CRITICAL` security alerts or transaction logs to jump the queue ahead of low-priority operational telemetry.
*   **Retry Engine & Exponential Backoff:** If socket transmission fails, the retry engine backs off using an exponential timing sequence with added random jitter:
    $$T_{\text{retry}} = 2^{\text{retry\_count}} \times \text{BaseInterval} + \text{Jitter}$$
    Retries are capped at a maximum of 300 seconds to prevent network flood upon reconnection.
*   **Dead Letter Queue (DLQ):** Payloads that repeatedly trigger server-side parsing errors (e.g., status 400 Bad Request) must be quarantined in a DLQ to prevent them from permanently blocking the queue delivery pipeline.
*   **Ordering Guarantees & Idempotency:** Financial and state transition events must execute sequentially. The server must enforce dynamic message sequence numbering (Monotonic Sequence IDs) to reject duplicate packets and execute commands in the exact sequence they occurred.
*   **Corrupted Queue Recovery Strategy:** If the local SQLite file becomes corrupted due to a sudden hard shutdown, the service will:
    1.  Immediately quarantine the corrupted database file (`offline_queue.db.corrupt`).
    2.  Instantiate a fresh, uncorrupted database skeleton.
    3.  Attempt to recover salvageable blocks from the corrupted database using a background recovery thread.
    4.  Log a high-severity alert indicating local database recovery was executed.

---

## PART 5 — ENTERPRISE ARCHITECTURE REVIEW

To achieve scalability across thousands of nodes, we must formalize our client-side patterns. The SAYRA WPF client and local SCM service must be decoupled via structured architectural patterns.

### 5.1 Architectural Decoupling Matrix

The table below outlines our Core Architectural Decisions for Phase 2:

| Component | Architecture Pattern | Why It Is Mandated | Alternatives Considered | Trade-offs & Risks |
| :--- | :--- | :--- | :--- | :--- |
| **Internal Communication** | **Internal Event Bus (Mediator)** | Decouples local modules. Allows Process Manager to dispatch state events without referencing the Logging or UI modules directly. | Direct Interface Injection | *Pros:* High decoupling.<br>*Cons:* Slightly harder debug tracking in IDE. |
| **Out-of-Process Communication** | **Message Dispatcher** | Handles structured JSON packet exchanges over local IPC Named Pipes safely. | Shared Memory (Memory Mapped Files) | *Pros:* Clean structured messaging.<br>*Cons:* Serialization overhead. |
| **Business State Logic** | **Domain Events** | Captures atomic business state changes (e.g. `UserLoggedOn`, `GameLaunched`). | Simple Method Invocation | *Pros:* Domain-driven clarity.<br>*Cons:* Increased boilerplate code files. |
| **Distributed State Alignment** | **Integration Events** | Handles state synchronization across network nodes (Client $\rightarrow$ Server $\rightarrow$ Admin Console). | Raw HTTP/TCP payloads | *Pros:* Standardization across APIs.<br>*Cons:* Demands precise cross-project schema syncing. |
| **Administrative Directives** | **Command Dispatcher** | Decodes incoming network directives (e.g. `Shutdown`, `Lock`) into transactional command tasks. | Direct socket action routing | *Pros:* Secure access control filters.<br>*Cons:* Overhead of message parsing pipeline. |
| **Synchronization** | **Background Sync Workers** | Executed in independent worker threads via .NET Generic Host, preventing UI lockups. | Inline UI thread execution | *Pros:* Non-blocking execution.<br>*Cons:* Requires thread safety locks. |
| **Data Ingestion** | **Upload Pipeline** | Batches, compresses, and schedules data uploads (telemetry, security alerts, audit events). | Direct instant API posts | *Pros:* Drastically reduces network traffic.<br>*Cons:* Delays telemetry rendering slightly. |
| **Asset Distribution** | **Download Pipeline** | Manages remote config sync assets, game patches, and ads with bandwidth limits. | WebClient / HttpClient directly | *Pros:* Respects network bandwidth rules.<br>*Cons:* High implementation complexity. |
| **Aggregation** | **Event Aggregator** | Combines low-level logs and high-frequency telemetry into unified batch snapshots. | Direct database writing | *Pros:* Minimized disk write wear.<br>*Cons:* Risk of data loss on sudden crash. |

---

## PART 6 — WINDOWS INTEGRATION

A high-performance workstation client cannot rely solely on platform-agnostic .NET framework wrappers. Deep native integration with the Windows Operating System is required to enforce policies, monitor resources, and handle system events.

### 6.1 Native Integration Specifications

```
┌────────────────────────────────────────────────────────────────────────┐
│                        WINDOWS OPERATING SYSTEM                        │
└─────┬──────────────────┬───────────────┬──────────────────┬────────────┘
      │                  │               │                  │
      ▼ (WinRT APIs)     ▼ (Win32 API)   ▼ (ETW / EvtLog)   ▼ (FileSystemWatcher)
┌───────────┐      ┌───────────┐   ┌───────────┐      ┌───────────┐
│ Windows   │      │ Windows   │   │ Windows   │      │ Registry  │
│ Toast     │      │ Session   │   │ Event Log │      │ File      │
│ Action Ctr│      │ Engine    │   │ & ETW     │      │ Watchers  │
└───────────┘      └───────────┘   └───────────┘      └───────────┘
```

1.  **Windows Notification APIs (Action Center):** Employs the `Windows.UI.Notifications` WinRT APIs to post system-level alerts to the Windows Action Center, ensuring visibility even when the WPF client application is minimized.
2.  **Windows Event Log & Event Tracing for Windows (ETW):** System errors, security bypass attempts, and Kiosk violations must be logged to the Windows Event Log (`Application` source: `SAYRA_Client`) alongside Serilog. ETW must be monitored for real-time tracking of hardware driver changes and network socket anomalies.
3.  **Windows Task Scheduler:** The client must register a fallback task in the Windows Task Scheduler configured to execute with highest privileges (`NT AUTHORITY\SYSTEM`). If the primary Windows Service is forcefully disabled or uninstalled, the scheduler automatically reinstalls and reboots the agent.
4.  **Background Tasks (WinRT / UWP API):** Registers a persistent background task that runs out-of-process to maintain heartbeat sync with the local server, even when the primary service is undergoing an update restart.
5.  **Windows Registry Watcher:** Setups a low-overhead file system monitor (`RegNotifyChangeKeyValue`) over crucial system shell registry hives (e.g., `HKLM\Software\Microsoft\Windows NT\CurrentVersion\Winlogon`). If a user attempts to manually reset the Windows Shell back to `explorer.exe` during lock state, the registry watcher instantly reverts the change and locks the system.
6.  **File System Watchers (`FileSystemWatcher`):** Monitors game directories and local client data files to detect tampering, blocking modification of game assets or user configuration binaries during active sessions.
7.  **Named Pipes Security Policies:** Communication over the local Named Pipes IPC (`\\.\pipe\SayraClientIpcPipe`) must be secured using discretionary access control lists (DACLs) that restrict read/write access strictly to the `LocalSystem` account and the currently authorized low-privilege interactive user SID.
8.  **Windows User Session Change Events:** Listens to session state notifications (`WTS_SESSION_LOCK`, `WTS_SESSION_UNLOCK`, `WTS_SESSION_LOGON`, `WTS_SESSION_LOGOFF`) via Win32 API callbacks, triggering dynamic state transitions inside the `TerminalStateManager`.

---

## PART 7 — RELIABILITY REVIEW

To maintain 24/7 reliability across thousands of public terminals, the system must employ defensive self-healing and recovery strategies.

### 7.1 Scenario Failure & Recovery Procedures

#### 1. Internet Disconnected
*   *System State:* WAN network unreachable; local LAN remains active.
*   *Recovery Strategy:* Local operations continue without interruption. Remote logging is diverted to local SQLite cache, and local authentication switches automatically to the LAN Master Server or local offline cache.

#### 2. Local Backend Server Unavailable
*   *System State:* Local Master Server offline; LAN remains active.
*   *Recovery Strategy:* The terminal switches to `Offline Grace Period`. Active user sessions remain unlocked. The local client computes session elapsed durations and costs using dynamic monotonic ticks (`QueryPerformanceCounter`) stored inside `session_state.json`.

#### 3. Administrative Panel Unreachable
*   *System State:* Admin panel crashes or goes offline.
*   *Recovery Strategy:* Workstations continue executing player sessions. Admin bypass modes fall back to local encrypted hash keys compiled into the agent binaries, allowing physical key/password bypasses.

#### 4. Client Application Crash / Restart
*   *System State:* WPF process crashes due to a UI error.
*   *Recovery Strategy:* The Session 0 service watchdog detects the WPF process exit, automatically queries the active session state from `session_state.json`, and spawns a fresh visual WPF Shell process inside the active user session token context within 500ms, preserving active gameplay.

#### 5. Windows Crash or Force Reboot
*   *System State:* Operating system undergoes BSOD or hard power cut.
*   *Recovery Strategy:* Upon boot, the service reads the transaction vault, detects the interrupted session, verifies if the elapsed downtime is within the allowable resume window (e.g. 10 minutes), and automatically resumes the session without requiring user re-authentication.

#### 6. Database / Queue Corruption
*   *System State:* Storage file write error corrupts the SQLite database.
*   *Recovery Strategy:* Quarantines the broken database, instantiates a clean database schema, and dispatches a background task to rebuild indices and repair salvageable entries from the quarantined file.

#### 7. Configuration Corruption
*   *System State:* The client config file `config.json` contains malformed JSON or invalid schema values.
*   *Recovery Strategy:* Restores the last known safe backup profile (`config.json.bak`) on startup and logs a configuration validation failure warning to the server.

#### 8. Partial Synchronization
*   *System State:* Network drops midway through configuration sync.
*   *Recovery Strategy:* All synchronized configurations are downloaded to a temporary file (`config.json.tmp`). The file is swapped to `config.json` only after a successful full payload download, SHA-256 hash match, and schema validation check.

#### 9. Duplicate Event Delivery
*   *System State:* Server retry logic dispatches duplicate messages.
*   *Recovery Strategy:* The message router validates event IDs against a sliding window cache, dropping duplicate packets before they are parsed by internal command dispatchers.

#### 10. Notification Delivery Failure
*   *System State:* Visual notification fails to render.
*   *Recovery Strategy:* Critical alerts log receipts to the `OfflineQueue`. If receipt confirmation is missing, the server re-transmits the notification through a secondary delivery channel (e.g. system broadcast message).

---

## PART 8 — PERFORMANCE REVIEW

An enterprise workstation agent must run without impacting game performance, minimizing CPU usage and memory footprints on high-end gaming rigs.

### 8.1 Scalability Impact Matrix

The table below outlines our performance targets and scale constraints:

| Metric | 100 Clients | 500 Clients | 1,000 Clients | 5,000 Clients |
| :--- | :--- | :--- | :--- | :--- |
| **Client Memory Target** | < 45 MB | < 45 MB | < 45 MB | < 45 MB |
| **Client CPU Target** | < 0.5% | < 0.5% | < 0.5% | < 0.5% |
| **Server Ingestion Load** | ~50 req/sec | ~250 req/sec | ~500 req/sec | ~2,500 req/sec |
| **WAN Bandwidth / PC** | < 1 kbps | < 1 kbps | < 1 kbps | < 1 kbps |
| **Log Upload Frequency** | Batch upload: every 10 minutes or 100 entries | Batch upload: every 10 minutes or 100 entries | Batch upload: every 15 minutes or 250 entries | Batch upload: every 30 minutes or 500 entries |
| **Batch Size (Telemetry)** | Max 50 items | Max 100 items | Max 250 items | Max 500 items |
| **Compression Strategy** | GZip (Default) | GZip (Default) | Brotli (Level 5) | Brotli (Level 5) |

### 8.2 Performance Best Practices & Resource Constrains
1.  **Zero Allocation Loops:** High-frequency telemetry samplers (e.g. tracking CPU and GPU usage) must avoid allocating new objects on every tick, minimizing garbage collection pauses that can cause in-game micro-stutters.
2.  **Thread Pool Offloading:** CPU-intensive cryptography checks, configuration parsing, and file integrity validations must execute on background ThreadPool threads rather than the UI thread or primary network socket listeners.
3.  **Structured JSON Decimation:** Metric collection must employ decimation algorithms, averaging data points locally before transmission to reduce database load.

---

## PART 9 — SECURITY REVIEW

Gaming center terminals are public devices exposed to constant security threats. The system must protect data authenticity, configuration integrity, and communication lines.

### 9.1 Threat Vectors & Architectural Mitigations

```
        ┌────────────────────────────────────────────────────────┐
        │                 MALICIOUS MAN-IN-THE-MIDDLE            │
        └───────────────────────────┬────────────────────────────┘
                                    │ (Attacks transport layer)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        SECURE TRANSPORT LAYER                          │
│  - Mandatory TLS 1.3 with Certificate Pinning                          │
│  - Dual Signatures (HMAC-SHA256) with rotating session keys            │
│  - Replay Attack Mitigation (Monotonic Sequence Numbers + Nonces)      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼ (Decrypts)
┌────────────────────────────────────────────────────────────────────────┐
│                       LOCAL CRYPTOGRAPHIC VAULT                        │
│  - Machine-specific DPAPI encryption keys                              │
│  - Database password locked via SQLCipher                              │
│  - Process execution sandboxing (Session Separation)                   │
└────────────────────────────────────────────────────────────────────────┘
```

1.  **Notification Integrity:** Prevents unauthorized screen takeovers or fake administrative prompts. Notifications must contain a signature hash verified against the pre-configured server public key (`server_public.key`).
2.  **Configuration Authenticity:** Protects against local policy modifications (e.g. disabling Kiosk mode). Configurations are cryptographically signed by the server. The client rejects any configuration file whose signature hash does not match the public key verification check.
3.  **Queue Encryption & Integrity:** The `OfflineQueue` database must be encrypted using AES-256 (SQLCipher) utilizing keys bound to the physical workstation motherboard (via Windows DPAPI). If the storage file is copied to another machine, the database remains unreadable.
4.  **Log Tampering Prevention:** Once a transaction or security audit event is written to the SQLite cache, the record is signed with a monotonic sequence number and an internal hash value. A user cannot delete or edit records inside the SQLite file without breaking the sequence chain.
5.  **Replay Protection:** All communication packets over HTTPS and the persistent TCP socket must incorporate a monotonic sequence counter and a rolling cryptographic nonce, preventing packet replay attacks.
6.  **TLS 1.3 and Certificate Pinning:** Communication with the central server must enforce TLS 1.3 with strict Certificate Pinning, preventing man-in-the-middle decryption via rogue network proxy drivers.

---

## PART 10 — FINAL GAP ANALYSIS

### 10.1 Complete Implementation Readiness Matrix

The following sections catalog all missing components, services, interfaces, and models required to build Phase 2 to enterprise-production specifications.

#### 1. Missing Components
*   `NotificationPriorityQueue` (Custom sorted in-memory queue mapping `Priority` properties).
*   `WpfNotificationOverlayWindow` (Semi-transparent TopMost overlay window rendering visual toasts).
*   `ClientServerTimeSynchronizer` (NTP network time synchronizer providing clock manipulation defense).
*   `ConfigDiffEngine` (Computes SHA-256 section hashes to execute delta configuration updates).
*   `MonotonicSequenceGenerator` (Tracks incoming and outgoing socket packets to prevent replay attacks).
*   `DistributedTracingContext` (Passes Correlation IDs, Session IDs, and Trace IDs across threads).

#### 2. Missing Services
*   `INotificationChannelService` (Manages channel delivery selections: WPF, Windows Action Center, or Audio).
*   `ISyncSchedulerService` (Governs jittered polling and push updates for configuration profiles).
*   `IEventBatchingService` (Compresses, buffers, and dispatches events to minimize socket activity).
*   `IQueueRecoveryService` (Diagnoses, repairs, and recovers corrupted SQLite queue databases).
*   `ISecureIpcPipeService` (Enforces DACL security rules over the Named Pipe communication lines).

#### 3. Missing Interfaces
```csharp
namespace Sayra.Client.Shared.Interfaces
{
    public interface INotificationChannel
    {
        NotificationChannelType ChannelType { get; }
        Task DeliverAsync(NotificationPayload payload);
        Task DismissAsync(string notificationId);
    }

    public interface IWorkstationSyncService
    {
        Task<SyncResult> ExecuteSyncAsync(SyncTriggerType triggerType);
        Task<bool> ApplyConfigDeltaAsync(string sectionName, string jsonDelta);
        Task<bool> RollbackToLastSafeConfigAsync();
    }

    public interface IOfflineQueue
    {
        Task EnqueueAsync(QueueItem item);
        Task<List<QueueItem>> DequeueBatchAsync(int batchSize);
        Task AcknowledgeBatchAsync(List<string> itemIds);
        Task QuarantineCorruptedItemsAsync();
    }
}
```

#### 4. Missing Models & DTOs
```csharp
namespace Sayra.Client.Shared.Models
{
    public enum NotificationPriority { SILENT, NORMAL, URGENT, CRITICAL }
    public enum NotificationChannelType { WPF_OVERLAY, WINDOWS_TOAST, SYSTEM_ACTION }

    public record NotificationPayload(
        string Id,
        string Title,
        string Body,
        string Category,
        NotificationPriority Priority,
        int TtlSeconds,
        string ActionCallbackToken,
        string LanguageToken,
        Dictionary<string, string> TemplateArgs
    );

    public record SyncDelta(
        string TargetSection,
        string SectionHash,
        string Base64DeltaPayload,
        long VersionCode
    );

    public record QueueItem(
        string Id,
        string Category,
        string PayloadJson,
        int RetryCount,
        DateTime CreatedAt,
        string SignatureHash
    );
}
```

#### 5. Missing Background Workers
*   `SyncSchedulerWorker` (Background host service managing scheduled configuration checks).
*   `EventQueueBatchingWorker` (Gathers event bus streams and flushes them in batches to the server).
*   `QueueTelemetryExporter` (Monitors local database sizing, write times, and retry metrics).

#### 6. Missing State Machines
*   `NotificationPresentationStateMachine` (Tracks `QUEUED` $\rightarrow$ `DISPLAYING` $\rightarrow$ `INTERACTED_WITH` $\rightarrow$ `DISMISSING` $\rightarrow$ `EXPIRED` states).
*   `SyncExecutionStateMachine` (Tracks `IDLE` $\rightarrow$ `VERSION_CHECKING` $\rightarrow$ `DOWNLOADING` $\rightarrow$ `VALIDATING` $\rightarrow$ `MERGING` $\rightarrow$ `APPLYING` $\rightarrow$ `FAILED_ROLLING_BACK` states).

#### 7. Missing Events
*   `ConfigurationSyncSucceededEvent` (Fires when a new configuration has been merged and applied).
*   `ConfigurationSyncFailedEvent` (Fires when validation fails and rollback is triggered).
*   `NotificationReceivedEvent` (Fires when a new notification has been routed to the presentation manager).
*   `QueueDiskLimitReachedEvent` (Fires when local SQLite database sizing breaches storage thresholds).

#### 8. Missing Configuration Objects
```json
{
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
  }
}
```

#### 9. Missing APIs & TCP Contracts
*   `POST /api/v1/workstations/{id}/sync/check` (Validates section hashes to return update deltas).
*   `POST /api/v1/workstations/{id}/notifications/acknowledge` (Dispatches notification read receipts).
*   `TCP_MSG: NOTIFICATION_BROADCAST (Code: 0x301)` (Dispatches push alerts to clients).
*   `TCP_MSG: FORCE_SYNC_TRIGGER (Code: 0x302)` (Forces clients to sync configurations instantly).

#### 10. Missing Pipelines
*   **Notification Delivery Pipeline:** Handles routing, priority sorting, rate limiting, and presentation rendering.
*   **Configuration Validation & Swap Pipeline:** Manages delta downloads, signature verification, schema validation, safe backup writing, and dynamic memory merges.
*   **Log Batch Upload Pipeline:** Handles log collection, compression, serialization, local buffering, and chunked transmission.

---

## PART 11 — IMPLEMENTATION ROADMAP & ROAD TO PRODUCTION

### 11.1 Priority & Implementation Matrix

| Code Component | Priority | Recommended Implementation Sequence | Estimated Sprint |
| :--- | :--- | :--- | :--- |
| **Encrypted SQLite Offline Queue** | **P0** | Essential for data integrity and offline recovery during network drops. | Sprint 1 |
| **Notification Router & Priority Queue** | **P0** | Required to safely route administrative alerts and session-end warnings. | Sprint 1 |
| **Configuration Signature & Integrity Verifier**| **P0** | Prevents security bypasses via MITM network packet modifications. | Sprint 1 |
| **WpfNotificationOverlayWindow UI** | **P1** | Essential to present session warnings and alerts directly to the user. | Sprint 2 |
| **Sync Scheduler & Dynamic Delta Merges** | **P1** | Preserves WAN bandwidth and optimizes updates across large workstation fleets. | Sprint 2 |
| **Log Rotation & Serialized Compression Pipeline**| **P1** | Prevents terminal storage exhaustion and optimizes diagnostic logs. | Sprint 2 |
| **Windows Notification & Registry Watcher API** | **P2** | Strengthens local workstation shell lockdown and security policies. | Sprint 3 |
| **Dynamic Localization & Token Template Engine** | **P2** | Matches multi-language requirements across diverse geographical regions. | Sprint 3 |
| **Sync State Metrics & Queue Sizing Exporter** | **P3** | Enhances system observability and performance telemetry. | Sprint 3 |

### 11.2 Suggested Sprint Breakdown (Phase 2)

#### Sprint 1 — Foundational Offline Resilience & Security
*   **Objective:** Implement secure local storage, the encrypted offline queue, command dispatchers, and signature validators.
*   **Deliverables:**
    1.  Encrypted SQLite Offline Queue using SQLCipher and Motherboard DPAPI bindings.
    2.  Notification Router & Priority Queue supporting silent vs visual alert segmentation.
    3.  Cryptographic Signature Verifier validating config payloads against `server_public.key`.

#### Sprint 2 — Synchronization and Diagnostic Pipelines
*   **Objective:** Build dynamic delta synchronization, configuration rollbacks, and serialized log rotation pipelines.
*   **Deliverables:**
    1.  Delta configuration comparison engine and dynamic merge strategies.
    2.  Configuration Rollback Engine restoring `config.json.bak` on validation failures.
    3.  Structured Serilog JSON rotation, local buffers, and batch compressed uploads.

#### Sprint 3 — UI Integration and Native Windows Policies
*   **Objective:** Implement visual presentation layers, dynamic localizations, and native Windows registry watchers.
*   **Deliverables:**
    1.  WpfNotificationOverlayWindow supporting high-dpi, RTL Persian, and topmost positioning.
    2.  Dynamic Localization translation merges for templates.
    3.  Windows Registry Watcher reverting shell changes and reporting Kiosk violations.

---

## PART 12 — ENTERPRISE READINESS SCORE

Based on enterprise specifications for large-scale gaming networks, Phase 2 is evaluated across key metrics:

*   **Security & Tamper Resistance (90%):** Strong signature verification, DPAPI bound SQLite encryption, and registry watchers prevent user bypasses.
*   **Reliability & Offline Survival (95%):** Encrypted offline queue, grace period countdowns, and automated rollbacks prevent commercial leakage.
*   **Scalability & Network Performance (85%):** Jittered scheduling, delta configurations, and Broti batching limit WAN bandwidth.
*   **Native Windows Integration (90%):** Named pipe DACLs, WinRT toasts, and session change hooks leverage Windows capabilities.
*   **Implementation Complexity (75%):** High requirement for robust multi-threaded design and thread safety patterns.

### Overall Enterprise Phase 2 Readiness Score: **88%**
*Verdict:* **Ready for Implementation.** The proposed specifications provide a robust, resilient, and secure blueprint for Phase 2 development.
