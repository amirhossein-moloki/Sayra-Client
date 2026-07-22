# SAYRA ENTERPRISE WINDOWS CLIENT — PHASE 1 ARCHITECTURAL REVIEW & GAP ANALYSIS
**Auditor:** Principal Enterprise Systems Architect
**Target Architecture:** Windows Client (.NET 8 Background Service & WPF Shell)
**Scale Assumption:** Enterprise-scale gaming center management system (Thousands of clients across multi-center environments)

---

## EXECUTIVE SUMMARY
This document presents an exhaustive architectural review and gap analysis of **Phase 1** of the SAYRA Enterprise Windows Client system. Phase 1 targets the foundational workstation layer, specifically addressing:
1. **Workstation State**
2. **Process Manager**
3. **Telemetry**
4. **Hardware Health**

As an enterprise-grade platform comparable to industry giants like GGLeap, Smartlaunch, SENET, and CyberCafePro, the SAYRA client must operate as a highly secure, reliable, high-performance, and resilient Windows Service (Session 0) integrated with a low-privilege interactive user interface (Session 1+).

This review assesses whether the initial Phase 1 modules are sufficient for enterprise operations, systematically decomposes each module into its logical constituents, analyzes individual components, justifies optimal architectural patterns, details deep-level Windows API integrations, audits telemetry and hardware monitoring pipelines, assesses process lifetime/security models, establishes state transition frameworks, formulates offline resilience profiles, evaluates scalability bounds, projects extensibility, computes a Production Readiness Score, and delivers concrete, actionable contracts, events, services, and architectural folder recommendations.

---

## PART 1: SUBSYSTEM ANALYSIS & MISSING ENTERPRISE SUBSYSTEMS

### 1.1 Is Phase 1 Sufficient?
**Verdict:** **No.** While the four modules (Workstation State, Process Manager, Telemetry, and Hardware Health) form the core execution loop of a localized terminal agent, they are entirely insufficient for an enterprise-level, multi-gaming center management platform. An enterprise product must ensure security, network resilience, physical station isolation, regulatory and commercial compliance, peripheral control, and zero-touch system administration. Without additional subsystems, a deployment of thousands of clients will suffer from system instability, rapid commercial leakage, high support costs, security vulnerabilities, and an unmanageable administrative burden.

### 1.2 Identification of Missing Subsystems

#### Subsystem A: Kiosk & Shell Lockdown Engine (KioskManager)
*   **Why It Is Required:** The Windows operating system by default allows users broad interactive permissions. In a public or commercial gaming lounge, users must be restricted from reaching administrative interfaces, command shells, registry tools, task manager, and operating system shortcuts (e.g., `Win+R`, `Ctrl+Alt+Del` options, `Alt+Tab` bypasses).
*   **Why Enterprise Products Have It:** CyberCafePro, GGLeap, and SENET enforce custom user shells. If a player bypasses the login window, they can access local storage, steal digital licenses, uninstall system software, or play games for free.
*   **What Problems Happen Without It:** Security compromise of the host OS, unauthorized system settings modifications, execution of malicious scripts (ransomware, miners), and direct theft of gaming sessions through explorer-based execution bypasses.

#### Subsystem B: Virtual Disk, Game Patching & Cache Sync Engine
*   **Why It Is Required:** Gaming installations span hundreds of gigabytes (e.g., Call of Duty, Cyberpunk). A local game library cannot function effectively if administrators must manually download updates on thousands of machines. An enterprise client requires a block-level virtual disk mounting system (iSCSI/PXE) or an automated peer-to-peer differential LAN patching engine.
*   **Why Enterprise Products Have It:** Enterprise gaming centers utilize diskless systems (PXE boot / virtual disks) or localized cache servers (e.g., GGLeap's local cache) to update games once on a master server and push them to thousands of workstations.
*   **What Problems Happen Without It:** High internet bandwidth utilization, unplayable games due to outdated client versions, fragmented local disks, manual labor bottlenecks for system administrators, and lost revenue when popular games are unavailable due to updates.

#### Subsystem C: Local Micro-Billing & Session Wallet Controller
*   **Why It Is Required:** Gamers operate on prepaid, postpaid, or recurring balance schemes. The client must maintain a high-precision, offline-resilient local billing counter that calculates costs, tracks session durations, and handles sudden monetary balance fluctuations in real-time.
*   **Why Enterprise Products Have It:** Smartlaunch and SENET maintain localized billing models that track elapsed time down to the millisecond. If the server goes offline, the workstation must calculate the exact usage and ensure the user's local balance cannot be overdrawn.
*   **What Problems Happen Without It:** Direct financial leakage, balance discrepancies between the database and local workstation session, immediate crash on network failure, or uncontrolled free gaming if the master billing API becomes unreachable.

#### Subsystem D: Peripheral & USB Access Controller (Hardware Guard)
*   **Why It Is Required:** Public computers are prime targets for malicious USB flash drives, hardware keyloggers, and mobile tethering bypasses (which allow clients to circumvent LAN web filtering).
*   **Why Enterprise Products Have It:** Enterprise-grade platforms implement device-arrival restrictions and USB class blocking (allowing only keyboards, mice, and standard headsets, while blocking mass storage and network adapters).
*   **What Problems Happen Without It:** Data exfiltration, injection of cheat software via rubber ducky devices, bypass of billing systems using USB tethering, and malware infections.

#### Subsystem E: Local Cryptographic Vault & Safe-State Engine
*   **Why It Is Required:** The client must securely store local workstation identities, session backup tokens, cached database schemas, and offline player hashes.
*   **Why Enterprise Products Have It:** GGLeap uses machine-specific hardware bindings (TPM/DPAPI) to encrypt configuration files, preventing players from reading or modifying local config files to grant themselves admin privileges or extra game time.
*   **What Problems Happen Without It:** Tampering with configurations, spoofing of station identities (e.g., claiming to be PC-01 while physically sitting at PC-45 to steal an active session), and exposure of local admin passwords stored in plain text configuration files.

#### Subsystem F: Remote Desktop & Screen Mirroring Engine
*   **Why It Is Required:** Administrators must assist players, debug issues, or monitor gameplay remotely from the admin console without physically visiting the client station.
*   **Why Enterprise Products Have It:** Modern gaming center platforms embed custom WebRTC or optimized VNC protocols to stream low-latency desktop feeds to the admin dashboard.
*   **What Problems Happen Without It:** Tremendous operational overhead, increased support response times, and an inability to resolve simple game configurations or system dialogs remotely.

---

## PART 2: DETAILED MODULE DECOMPOSITION

Below is the architectural breakdown of the four core Phase 1 modules into fine-grained components.

```
[SAYRA CLIENT - PHASE 1 CORE MODULES DECOMPOSITION]
├── 1. WORKSTATION STATE MODULE
│   ├── State Detection Engine (System Boot, User Interaction, Idle Hooks)
│   ├── State Transition Coordinator (Thread-safe state change router)
│   ├── Idle Detection Monitor (Low-level mouse/keyboard input hooks)
│   ├── Lock State Controller (Desktop switching & Kiosk locking API)
│   ├── Reservation State Monitor (Pre-allocation lock interface)
│   ├── Session State Engine (Active session counters, local persistence)
│   ├── Network State Watcher (Socket state, LAN quality, dynamic disconnect adapters)
│   ├── Power State Handler (OS-level Power Broadcast listener, Shutdown/Restart)
│   ├── Maintenance State Supervisor (Administrator bypass mode & policy suspension)
│   └── Error State Mediator (Self-healing & fallback recovery executor)
│
├── 2. PROCESS MANAGER MODULE
│   ├── Process Discovery & Registry Scanner (WMI/Win32 snapshot query)
│   ├── Executable Integrity Validator (SHA-256 Hash, Authenticode Signatures)
│   ├── Process Injection Guard (DLL hook monitor, anti-cheat helper)
│   ├── Crash Detection Sentinel (Process lifetime handle tracker & exit monitor)
│   ├── Restart Policy Engine (Backoff timing logic, retry counters)
│   ├── Whitelist / Blacklist Policy Enforcer (Execution block hooks)
│   ├── Foreground Window Tracker (Focus monitoring, game telemetry capture)
│   ├── Child Process Tree Monitor (WMI/Win32 Job Object tracker)
│   ├── Zombie & Leak Monitor (Handle counting, memory/CPU bounds checker)
│   ├── Resource Abuse Throttle (Process priority class modifier)
│   ├── Anti-Cheat Integrator (Compatibility engine with EasyAntiCheat/BattlEye)
│   ├── Launch Environment Prep Engine (Registry virtualizer, dynamic file mapping)
│   └── Lifetime Event Publisher (Structured logging & socket events dispatcher)
│
├── 3. TELEMETRY MODULE
│   ├── Metric Collection Coordinator (Scheduling, scheduling strategies)
│   ├── System Metric Harvester (OS-level uptime, context switches, interrupt rates)
│   ├── Game Metric Harvester (Process-bound CPU, RAM, active handles, VRAM)
│   ├── Performance Metric Poller (Hardware-level continuous sampling)
│   ├── Network Latency Probe (ICMP ping, socket round-trip-time analyzer)
│   ├── Frame Rate (FPS) Monitor (DirectX / Vulkan / OpenGL presenter hooks)
│   ├── Crash Dump Capturer (Windows MiniDumpWriteDump integration)
│   ├── Log Aggregation Service (Serilog file, Sinks, OS Event Log scraper)
│   ├── Data Aggregator & Compactor (Moving-average, delta compression)
│   ├── Batch Upload Dispatcher (Encrypted TLS HTTP/TCP socket buffer)
│   ├── Privacy & Redaction Engine (Anonymizer for PII in dump files)
│   └── Offline Storage Manager (SQLite / Encrypted JSON cache fallback)
│
└── 4. HARDWARE HEALTH MODULE
    ├── Hardware Topology Explorer (WMI Spec query, SMBIOS Parser)
    ├── Polling Scheduler (Adaptive rates based on system state)
    ├── Temperature Probe (CPU/GPU core temp via WMI/MSAcpi_ThermalZoneTemperature)
    ├── Memory Diagnostics Monitor (Available physical memory, page file status)
    ├── Disk Health Sentinel (S.M.A.R.T. attributes, disk queue lengths)
    ├── Power Supply & Battery Watcher (ACPI state, power consumption)
    ├── Cooling Fan Speed Tracker (Fan RPM query via custom driver / WMI)
    ├── VRAM Capacity Inspector (DXGI adapter query)
    ├── Network Interface Health Monitor (Packet loss, link speed negotiation)
    ├── BIOS & Driver Audit Engine (Firmware, driver signing level query)
    ├── Device Arrival & Removal Monitor (USB, monitor, audio peripheral state changes)
    ├── Threshold Violation Evaluator (Critical temp / usage limit warning trigger)
    ├── Performance Impact Controller (Telemetry throttling on low-end systems)
    └── Recovery & Self-Healing Handler (Automatic driver/device reset, error alert)
```

---

## PART 3: COMPONENT-LEVEL SPECIFICATIONS

For brevity and focus, we present detailed specifications for three core architectural components across these modules, demonstrating the pattern for all:

### 3.1 Lock State Controller (Workstation State)
*   **Responsibilities:** Enforces the visual kiosk locked screen, intercepts operating system shortcuts, prevents users from accessing Windows Explorer, and manages custom secure desktop isolation.
*   **Inputs:** `LockCommand`, `UnlockCommand`, Kiosk Configuration profile, User Context object.
*   **Outputs:** `LockStateChangedEvent`, Lock Screen Window handle, Registry modifier success flags.
*   **Dependencies:** `KioskManager` (Core), User32.dll (Win32 API), Gdi32.dll, Advapi32.dll (Registry access).
*   **Failure Cases:**
    1.  *Failure 1:* Windows Explorer fails to terminate or spawns in the background.
    2.  *Failure 2:* Low-level keyboard hook fails to register (anti-virus intercepts the hook).
*   **Recovery Strategy:** If low-level hooks fail, the controller implements a secondary desktop switcher using `CreateDesktop` / `SwitchToDesktop` API, creating a distinct "SayraSecureDesktop" isolated from default Windows controls.
*   **State Transitions:** `State: READY (Unlocked)` $\rightarrow$ `State: LOCKED (Locked Desktop Enabled)`.
*   **Performance Considerations:** Negligible CPU impact (<0.5%). Memory overhead must remain stable (<20MB) because the lock screen window is persistently rendered.

### 3.2 Executable Integrity Validator (Process Manager)
*   **Responsibilities:** Verifies the safety and cryptographic integrity of any game or administrative tool before spawning.
*   **Inputs:** Executable Path, Valid Hash Database, Signature Requirement profile.
*   **Outputs:** `ValidationResult` (IsValid, CertificateSubject, SHA256Hash, IsSigned).
*   **Dependencies:** `System.Security.Cryptography`, Win32 Authenticode API (`WinVerifyTrust`).
*   **Failure Cases:**
    1.  *Failure 1:* Verification times out on slow mechanical drives or network storage paths.
    2.  *Failure 2:* File access permissions block signature read operations.
*   **Recovery Strategy:** Caches valid hashes in an encrypted local database (`hash_cache.db`). If disk reading times out, falls back to the local cache if the file metadata (CreationDate, Size) has not changed.
*   **State Transitions:** None. Pure functional component.
*   **Performance Considerations:** Standard hashing is CPU-bound. Game executable checks (often multiple gigabytes) must only target the primary launcher binary, never the entire directory structure. Background thread pool execution is mandatory.

### 3.3 Temperature Probe (Hardware Health)
*   **Responsibilities:** Continuously queries system thermal states across CPU cores, GPU units, and NVMe drives to prevent physical damage.
*   **Inputs:** Polling Interval, Critical Thermal Threshold Profile.
*   **Outputs:** `ThermalSnapshot` (CPU temp, GPU temp, Disk temp, Ambient temp).
*   **Dependencies:** WMI (`MSAcpi_ThermalZoneTemperature`, `root\wmi`), NVIDIA NVML APIs, AMD ADL APIs.
*   **Failure Cases:**
    1.  *Failure 1:* Access denied to hardware motherboard driver sensors.
    2.  *Failure 2:* WMI query locks up (blocking the thread for >10 seconds).
*   **Recovery Strategy:** Fall back to platform-agnostic diagnostics APIs (such as OpenHardwareMonitor library extensions) or GPU driver-specific command-line helpers. If WMI queries block, timeout the task at 1.5 seconds and mark WMI as degraded, utilizing the last known safe state.
*   **State Transitions:** None. Continuous poller.
*   **Performance Considerations:** WMI querying has high performance costs. Temperature polls should be executed on a dedicated background thread with an adaptive sliding rate (e.g., poll every 2 seconds during active gaming sessions, but throttle to 15 seconds when idle/locked).

---

## PART 4: ARCHITECTURAL REVIEW & PATTERN JUSTIFICATION

```
                           +-------------------------------------+
                           |            SAYRA SERVER             |
                           +-------------------------------------+
                                      ^               ^
                    TCP (Secure TLS)  |               |  HTTPS (APIs)
                                      v               v
+---------------------------------------------------------------------------------+
|                                 SAYRA CLIENT                                    |
|                                                                                 |
|  +---------------------------------------------------------------------------+  |
|  |                        CORE WINDOWS SERVICE (Session 0)                   |  |
|  |                                                                           |  |
|  |  +---------------------------+             +---------------------------+  |  |
|  |  |  State Machine (Stateless)| <---------> |  Command Dispatcher (Bus) |  |  |
|  |  +---------------------------+             +---------------------------+  |  |
|  |                ^                                         ^                |  |
|  |                |                                         |                |  |
|  |                v                                         v                |  |
|  |  +---------------------------+             +---------------------------+  |  |
|  |  | Background Schedulers/Wrkr|             | Local SQLite/Encrypted DB |  |  |
|  |  +---------------------------+             +---------------------------+  |  |
|  +---------------------------------------------------------------------------+  |
|                                     ^                                           |
|                                     | Bi-directional IPC (Named Pipes)          |
|                                     v                                           |
|  +---------------------------------------------------------------------------+  |
|  |                             WPF SHELL (Session 1)                         |  |
|  |                                                                           |  |
|  |                    +--------------------------------+                     |  |
|  |                    |    Observer-driven ViewModels  |                     |  |
|  |                    +--------------------------------+                     |  |
|  +---------------------------------------------------------------------------+  |
+---------------------------------------------------------------------------------+
```

### 4.1 Event-Driven Architecture (EDA)
*   **Verdict:** **Yes, Mandatory.**
*   **Justification:** An enterprise workstation client handles non-deterministic physical and network interactions. A game crash, network drop, user mouse movement, or remote admin shutdown can occur at any moment. Emulating these as blocking synchronous calls will crash the client. EDA using `System.Reactive` (Rx.NET) allows the system to treat telemetry, crash, and keyboard events as streams, filtering and mapping them asynchronously without blocking the primary threads.

### 4.2 Observer Pattern
*   **Verdict:** **Yes, Mandatory.**
*   **Justification:** The UI viewmodel must dynamically react to background processes, network socket states, and timer ticks. The observer pattern (implemented natively in WPF via `INotifyPropertyChanged` and `IObservable<T>`) decouples the background Windows Service state from the presentation rendering, preventing UI freezing.

### 4.3 Message Bus (Mediator Pattern)
*   **Verdict:** **Yes, Mandatory.**
*   **Justification:** Inter-module coupling is the single largest cause of technical debt in desktop systems. The Process Manager should not need to directly reference the Telemetry module to report a launch event. Registering an in-memory Message Bus (e.g., using MediatR or a custom lightweight `InMemoryEventBus`) permits loose coupling. The Process Manager publishes `ProcessLaunchedEvent`, and both the Telemetry and Session modules independently consume it.

### 4.4 Hierarchical State Machine (HSM)
*   **Verdict:** **Yes, Mandatory.**
*   **Justification:** Managing states like `READY`, `LOCKED`, `IN_SESSION`, `PLAYING`, `MAINTENANCE`, and `RECOVERING` requires formal structural validation. An ad-hoc state pattern utilizing simple booleans (e.g., `bool isLocked`, `bool isPlaying`) creates invalid states (such as locked and playing at the same time). We mandate a formal State Machine framework (e.g., Stateless library) with strict, validated transition policies, guards, and enter/exit actions.

### 4.5 Background Workers & Schedulers
*   **Verdict:** **Yes, Mandatory.**
*   **Justification:** Background workers (implementing .NET `IHostedService` or running inside task pools) are required to offload long-running operations. CPU/RAM metric collection, server heartbeats, and disk health monitoring must run independently of the main UI thread and the primary socket worker thread.

### 4.6 Heartbeat Service
*   **Verdict:** **Yes, Mandatory.**
*   **Justification:** With thousands of clients connected across a WAN, the server must immediately detect a client disconnection. A bi-directional heartbeat (ping/pong frame) executing every 5 to 10 seconds over the persistent TCP socket allows both the client and server to immediately determine socket state transitions, preventing session hanging and stale terminal states.

### 4.7 Local Offline Cache (Encrypted SQLite)
*   **Verdict:** **Yes, Mandatory.**
*   **Justification:** Enterprise clients must not fail when the LAN switch or internet disconnects. An encrypted local storage vault (using SQLite with SQLCipher or DPAPI-secured local files) is required to cache active user sessions, local configurations, and audit events, ensuring they can be re-synced once connection is restored.

### 4.8 Command Dispatcher
*   **Verdict:** **Yes, Mandatory.**
*   **Justification:** Remote commands (e.g., `REBOOT`, `TERMINATE_GAME`, `SEND_ALERT`) arrive via the server socket. A command dispatcher parses these payloads, instantiates specific execution context command handlers, validates administrative privileges, and routes them to target modules safely.

---

## PART 5: DEEP WINDOWS INTEGRATION AUDIT

Executing gaming center management on Windows requires deep integrations with the Win32 API and OS subsystems.

### 5.1 System Service Control Manager (SCM) & Windows Service
*   **Integration Detail:** The core client agent runs as a system service in **Session 0** under the `LocalSystem` account. This ensures it starts prior to user logon, remains un-killable by standard users, and has the permissions required to modify registry files, enforce group policies, and manage machine-wide processes.

### 5.2 Desktop Session Isolation & Session Change Monitoring
*   **Integration Detail:** In Windows Vista and newer, Session 0 isolation prevents services from directly displaying user interfaces. The service must monitor desktop interactive session changes using the WTSRegisterSessionNotification API and spawn the low-privilege WPF Shell inside the active user interactive session (typically Session 1) using the `CreateProcessAsUser` API with target session tokens.

### 5.3 Low-Level Keyboard Hooks (WH_KEYBOARD_LL)
*   **Integration Detail:** Intercepting system shortcuts like `Alt+Tab`, `Alt+F4`, `Win+L`, `Win+D`, and `Ctrl+Esc` requires registering a low-level keyboard hook using `SetWindowsHookEx`. These hooks must return `1` (indicating handled) to block the OS from processing the key combinations. *Note: `Ctrl+Alt+Del` cannot be blocked via hooks; it is handled by switching secure desktops or custom GINA/Credential Provider filters.*

### 5.4 Windows Job Objects API
*   **Integration Detail:** Gamers often run launchers (Steam, Epic) which spawn multiple child processes. If the client kills only the parent process on session timeout, child processes (the actual game binaries) remain running. By encapsulating every launched game process tree inside a Windows **Job Object** (`CreateJobObject`, `AssignProcessToJobObject`), the Process Manager can terminate the entire tree cleanly using `TerminateJobObject` with zero zombie processes remaining.

### 5.5 Registry Policy Restrictions (GPO Emulation)
*   **Integration Detail:** The client must programmatically edit local registry keys in real-time to lock down system controls without active Active Directory domains:
    *   *Disable Task Manager:* Set `Software\Microsoft\Windows\CurrentVersion\Policies\System\DisableTaskMgr` to `1`.
    *   *Disable Registry Tools:* Set `DisableRegistryTools` to `1`.
    *   *Restrict Control Panel:* Set `NoControlPanel` to `1`.
    *   *System Command Blocks:* Lock down Win+R using policies.

### 5.6 Power Management & ACPI Power Events
*   **Integration Detail:** The client service must register for power broadcast notifications via `RegisterPowerSettingNotification`. This allows the service to intercept sleep/hibernate states (`PBT_APMSUSPEND`) and lock the session, ensuring players do not bypass the billing system by putting the workstation to sleep.

### 5.7 USB Device Arrival & PNP Notification APIs
*   **Integration Detail:** The service must hook into the window message loop (`WM_DEVICECHANGE`) to track device arrivals. Utilizing the SetupAPI and WMI `Win32_PnPEntity`, the system identifies class GUIDs of plugged-in devices, immediately disabling mass storage, unauthorized network adapters, or potential injection hardware.

---

## PART 6: HARDWARE MONITORING PIPELINE

To manage thousands of machines across high-end PC gaming cafes, hardware diagnostics must be exceptionally granular yet resource-efficient.

### 6.1 Harvester Target Matrix & Polling Strategies

| Component | Harvester Metric | Query Target Mechanism | Polling Rate (In Session) | Threshold (Critical Warning) | Performance Overhead Mitigation |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **CPU** | Temp, Load, Clock | WMI / OpenHardwareMonitor API | 2 Seconds | Temp > 85°C / Load > 98% | Cache static structures; only query dynamic values |
| **GPU** | Temp, Load, VRAM | NVML (Nvidia) / ADL (AMD) | 2 Seconds | Temp > 83°C / VRAM > 95% | Load DLLs on demand; avoid WMI wrapper overhead |
| **RAM** | Load, Free MB, Swaps | GlobalMemoryStatusEx | 5 Seconds | Available < 10% | Direct Win32 memory structure query (under 1ms) |
| **Disk** | Disk Active %, Queue | Performance Counters | 10 Seconds | Queue > 3.0 for 30s | Zero-allocation PerfCounter reading |
| **Disk SMART** | Health, Bad Sectors | WMI `MSStorageDriver_FailurePredictStatus` | 300 Seconds | PredictStatus == True | Run once on lock state, run rarely in active session |
| **Motherboard**| Voltage, Fans | WMI Hardware Sensors | 10 Seconds | Fan < 500 RPM / Volt > 10% Var | Direct vendor API bindings, query throttle |
| **Peripherals**| USB Device Count | Device arrival notifications | Event-driven | Unauthorized USB Class ID | Zero polling; pure push via event callback loop |

### 6.2 Polling Strategy Optimization & Sampling Rates
Querying hardware metrics can easily exhaust CPU cycles. An enterprise client implements an **Adaptive Polling Strategy**:
*   *Interactive Gaming State:* Temp and usage polled every 2-5 seconds to safeguard expensive hardware (GPUs/CPUs) from thermal throttling or fan failure.
*   *Locked / Idle State:* Polling is dynamically throttled to every 30-60 seconds to minimize system activity and power consumption.
*   *Window Filter (Moving Average):* To prevent transient spikes from triggering false alarms (e.g., a 100% CPU spike for 1 second during game load), metrics are parsed through a 10-sample moving average window.

---

## PART 7: ENTERPRISE PROCESS MANAGER SPECIFICATION

The Process Manager is the core execution system of the workstation. It handles launch pre-requisites, active monitoring, and immediate cleanups.

```
       [Launch Request]
              |
              v
     [Is Executable Allowed?] ---> (No) ---> [Block & Alert]
              |
              v (Yes)
  [Validate File Integrity]
     - Authenticode Signature Check
     - SHA-256 Hash Verification
              |
              v (Valid)
   [Prepare Launch Context]
     - Virtualize registry keys
     - Generate secure sandbox environmental parameters
              |
              v
    [Assign to Job Object]
              |
              v
     [Spawn Game Process]
              |
              +---------------+---------------+
              |               |               |
              v               v               v
       [Crash Sentinel]  [Leak Monitor]  [Focus Tracker]
```

### 7.1 Executable Validation & Sandboxing
Before spawning a process, the manager validates Authenticode certificates via `WinVerifyTrust`. It blocks unsigned executables unless they are matched against a strict administrator whitelist hash. To support diverse game distribution mechanisms, the manager injects virtual registry files to align user paths with actual local directories without modifying physical host OS keys.

### 7.2 Detection of Process Injection & Cheats
The Process Manager monitors the active game handles using API hooks and `PsSetCreateProcessNotifyRoutine` (if backed by a kernel driver). It flags unauthorized attempts to invoke `VirtualAllocEx` or `WriteProcessMemory` against game processes, blocking malicious injection payloads and ensuring anti-cheat engines (e.g., EasyAntiCheat, BattlEye, Vanguard) are not compromised.

### 7.3 Crash Detection & Smart Recovery Policy
Processes are monitored continuously using process handles (`GetExitCodeProcess`).
*   **Crash State:** Process terminates with a non-zero exit code or fails within 60 seconds of launching.
*   **Restart Policy:** Employs an exponential backoff retry mechanism (e.g., Retry 1 at 2 seconds, Retry 2 at 5 seconds, Retry 3 at 10 seconds). Retries are capped at a maximum of **3**. If it fails a 3rd time, the game status is set to `FAILED_TO_START`, the session remains active but locked out of that game, and a critical alert is dispatched to the Admin Panel.

### 7.4 Whitelist, Blacklist, & Resource Abuse Control
*   **Whitelist:** Block all process execution events outside specified folders and hashes.
*   **Blacklist:** Enforces continuous scanning of active window titles and process names. It instantly kills banned software (e.g., cheat software, network sniffers, torrent clients).
*   **Resource Throttling:** If a game process attempts to exhaust CPU or memory bounds while a player is in a lobby (idle state), the manager scales down process priorities (`IdlePriorityClass`) or constrains CPU cores using `SetProcessAffinityMask`.

---

## PART 8: ENTERPRISE TELEMETRY PIPELINE

Telemetry must provide deep operational insights without overwhelming network infrastructure.

### 8.1 Metric Collection Taxonomy
*   **System Metrics:** Boot time, physical memory utilization, page file faults, packet loss percentage, link latency, OS error event logs.
*   **Game Metrics:** Session duration, exact PID, CPU/RAM utilization curves, VRAM usage, average FPS, frame drops, exit codes, crash records.
*   **User Action Audit Log:** Screen unlocks, failed login attempts, admin overrides, launcher button clicks, local backup operations.

### 8.2 Performance and Network Management

```
[Raw Metrics] -> [Aggregation Engine] -> [Delta Compression] -> [Local Cache] -> [TLS Batch Dispatch]
```

*   **Aggregation & Decimation:** Telemetry is gathered locally every 2 seconds but is aggregated into 1-minute averages prior to dispatching, reducing metric payload size.
*   **Delta Compression:** The system transmits only the differences (deltas) from the previous report. If the CPU load is unchanged, the value is omitted in the subsequent message.
*   **Offline Cache Fallback:** If connection is lost, telemetry is cached in an encrypted local database (`telemetry_buffer.db`). To prevent storage exhaustion, the cache enforces a FIFO ring buffer capped at 500MB (approximately 48 hours of continuous telemetry).
*   **Privacy & PII Redaction:** The client automatically strips user folder names, system usernames, IP configurations, and clipboard states from system logs and memory dump files (`MiniDumpWriteDump`) before server dispatch.

---

## PART 9: WORKSTATION STATE MACHINE & LIFECYCLE

```
                              +---------------------------------------+
                              |               STARTING                |
                              +---------------------------------------+
                                                  |
                                                  | Initialize Configuration
                                                  v
                              +---------------------------------------+
                              |              DISCOVERING              |
                              +---------------------------------------+
                                                  |
                                                  | Server Beacon Found
                                                  v
                              +---------------------------------------+
                              |              CONNECTING               |
                              +---------------------------------------+
                                                  |
                                                  | TCP Handshake Complete
                                                  v
                              +---------------------------------------+
                              |                 READY                 | <-----------------------+
                              +---------------------------------------+                         |
                                 |                                 |                            |
                                 | Gamer Login                     | Maintenance Bypass         |
                                 v                                 v                            | Session End /
                              +---------------+             +---------------+                   | Lockout
                              |  IN_SESSION   |             |  MAINTENANCE  |                   |
                              +---------------+             +---------------+                   |
                                 |          ^                      |                            |
                   Launch Game   |          | Game Ended           | Reset Admin Mode           |
                                 v          |                      |                            |
                              +---------------+                    |                            |
                              |    PLAYING    | -------------------|----------------------------+
                              +---------------+                    |
                                 |                                 |
                                 | Unexpected Crash                |
                                 v                                 |
                              +---------------+                    |
                              |  CRASH_RECOV  | -------------------+
                              +---------------+
```

### 9.1 Exhaustive State Dictionary

1.  **STARTING:** Initial power-up. Core configurations are parsed, memory checks performed, and base system integrity validated. (Timeout: 30 seconds $\rightarrow$ transitions to `ERROR` if database is unreadable).
2.  **DISCOVERING:** Emits secure UDP broadcasts on LAN to locate the Master Server. (Timeout: None. Continuously broadcasts with progressive backoff).
3.  **CONNECTING:** Found the server. Establishes TCP connection and negotiates AES-256 session keys. (Timeout: 10 seconds $\rightarrow$ falls back to `DISCOVERING` on socket failure).
4.  **READY:** Workstation is active, network is verified, and the login shell is displayed. Host OS is fully locked (Kiosk active).
5.  **IN_SESSION:** Player successfully logged in. Billing counters start, OS policies are unlocked, and the game launcher is active.
6.  **PLAYING:** Game execution active. Process monitors tracking the child tree, and telemetry gathering is escalated to peak polling rates.
7.  **CRASH_RECOVERING:** Game process ended unexpectedly. The Process Manager attempts automated execution retries, and the UI displays loading feedback.
8.  **MAINTENANCE:** Administrator has signed in with bypass credentials. Security policies are suspended, Windows Explorer is launched, and full diagnostic/scaffold tasks are enabled.
9.  **RECOVERING / DISCONNECTED:** Connection lost during active gameplay. Grace-period countdown starts (e.g., 5 minutes) allowing offline play. If timeout expires without server reconnection, the system forces session cleanup and transitions to `LOCKED`.

### 9.2 Conflict Resolution & Prioritization
*   **Rule 1:** Remote Server commands always supersede local state transitions. If the Server dispatches a `FORCE_LOCK` message, active games are immediately terminated, even if the player is in an active session.
*   **Rule 2:** Physical hardware overrides (such as Administrator physical smart card insertion or local password bypass) override server lockdowns to allow emergency manual control during complete network failures.

---

## PART 10: SECURITY REVIEW & THREAT MODELING

Gaming centers are highly adversarial environments. Players regularly attempt to hack clients to play without paying or to run malicious tools.

### 10.1 STRIDE Threat Model & Mitigations

| Threat | Description | Specific Mitigation Strategy |
| :--- | :--- | :--- |
| **Spoofing** | Player attempts to mimic the admin server to force workstation unlock. | Dual HMAC-SHA256 frame signature validation using rotating dynamic session keys generated on handshake. |
| **Tampering** | Player edits local `client_config.json` to change station ID or disable Kiosk mode. | Validate hashes of config files against a server master; encrypt local databases using platform-specific DPAPI keys. |
| **Repudiation** | Player claims they did not start a session or run a billing event. | Encrypted local SQLite transaction logs with monotonic cryptographic sequence counts. |
| **Info Leak** | Player reads RAM or physical configurations to extract server database passwords. | Strict privilege separation; run UI shell in low-privilege User Session context with zero access to Session 0 credentials. |
| **Denial of Svc**| Player floods UDP or local Named Pipes with trash packages. | Limit Named Pipe ACLs strictly to the Local System and authorized User SIDs; reject unauthenticated packets immediately. |
| **Elevation** | Player bypasses the WPF shell to open CMD/Powershell as Administrator. | Service executes on Session 0; Interactive UI executes strictly in the user's low-privilege security token context. |

### 10.2 Tamper Protection & Code Integrity
The Windows Service includes a secure Watchdog thread. If the visual UI process (`Sayra.UI.exe`) is terminated by a task killer or a process injector, the Service instantly detects the exit handle, logs an audit alert to the server, and spawns a fresh instance of the UI in secure lock mode within 500ms.

---

## PART 11: OFFLINE MODE RESILIENCE

An enterprise workstation must remain fully operational and commercially secure during network failures.

```
+-------------------+      TCP Lost      +------------------------+      Timeout      +--------------------+
|  ONLINE SESSION   | -----------------> | OFFLINE GRACE PERIOD   | -----------------> | FORCE HARD LOCKOUT |
|  - Real-time Sync |                    | - Local SQLite Billing |      Expired       | - Terminate games  |
+-------------------+                    | - Queue events locally |                    | - Show Lock Screen |
                                         +------------------------+                    +--------------------+
                                                     |
                                                     | TCP Re-established
                                                     v
                                         +------------------------+
                                         | STATE RECONCILIATION   |
                                         | - Flush offline queue  |
                                         | - Sync timer delta     |
                                         +------------------------+
```

### 11.1 Failure Matrix & Recovery Procedures

#### Case A: Internet Disconnection
*   *Immediate Outcome:* Local LAN connection to Master Server remains active.
*   *Behavior:* System operates normally. Client transitions to a warning state indicating cloud capabilities (e.g., online matchmaking profiles) are offline, but billing and local game execution are unaffected.

#### Case B: Complete LAN Server Disconnection
*   *Immediate Outcome:* Secure TCP socket drops.
*   *Behavior:* The client transitions to `OFFLINE_GRACE` mode. A 5-minute countdown is displayed to the user. The workstation maintains billing counters inside local encrypted state files (`session_state.json`). Games remain playable during this window. If the connection is not restored within 5 minutes, the client terminates all games and forces a hard lock screen.

#### Case C: Workstation Power Outage or Force Reboot
*   *Immediate Outcome:* Client machine powers down immediately without sending final states to the server.
*   *Behavior:* Upon power restoration, the SCM launches the Windows Service first. Before launching the UI, the Service reads the local transaction vault (`Data/session_state.json`). It detects the interrupted active session, calculates the elapsed power-off delta against the local system clock, verifies clock tamper flags, and automatically restores the active session without requiring player re-authentication.

#### Case D: Local Clock Manipulation
*   *Immediate Outcome:* Player attempts to set system time back to gain free play time.
*   *Behavior:* The billing system does not rely on Windows System Time alone. It queries the monotonic system processor tick counts (`QueryPerformanceCounter` / `Environment.TickCount64`) to compute elapsed duration, completely bypassing system clock manipulations. If a system clock drift of >10 seconds is detected against the processor tick, the client logs a critical clock tampering alert to the server.

---

## PART 12: SCALABILITY & CONCURRENCY PROFILE

Managing thousands of clients requires careful optimization of communication protocols.

```
       [Client Scale Optimization Strategy]
┌───────────────────────────────────────────────────┐
│     5,000 PCs Telemetry Load (Raw vs Optimized)   │
├───────────────────────────────────────────────────┤
│ Raw Polling (1s Interval):  5,000 req/sec         │
│ -> Network Saturation & Server Database Lockup     │
├───────────────────────────────────────────────────┤
│ Optimized Strategy (Push-only/Delta Batches):     │
│ - Heartbeat: 10s interval (lightweight payload)   │
│ - Telemetry: 30s batch intervals (highly compressed)│
│ - Game Logs: Event-driven (Instant but sparse)    │
│ -> Aggregate Network Load reduced by 92%          │
└───────────────────────────────────────────────────┘
```

### 12.1 Network Optimization under Scaled Deployments
If 5,000 clients send detailed telemetry every second, the server must process 5,000 requests per second. This results in high database lockup rates and network congestion.
*   **Heartbeat Strategy:** Thin UDP ping packets every 10 seconds. The server responds with a simple acknowledge byte.
*   **Dynamic Telemetry Batching:** Telemetry metrics are buffered locally and dispatched via a single compressed JSON package (GZIP / Deflate) over the TCP socket every 30 seconds.
*   **Push-Driven Architecture:** The client never polls the server for status updates. State synchronization is strictly push-driven; the server pushes command frames to the persistent client socket only when actions are required, dropping background WAN utilization to under 1.5kbps per terminal.

---

## PART 13: EXTENSIBILITY ANALYSIS

We evaluated whether this Phase 1 architecture can support future planned modules without major refactorings.

### 13.1 Plugin & Modularity Support
*   *Analysis:* By integrating a strict Dependency Injection registration system, adding future modules is straightforward. For example, adding a custom **Overlay Module** requires registering an `IOverlayService` that subscribes to the existing `IGameLauncherService.GameStarted` event. This requires zero changes to the core process manager or state machines.

### 13.2 Remote Commands, Patching, & Remote Control
*   *Analysis:* Remote commands are supported natively by our `CommandDispatcher`. A future **Patch Engine** simply registers a new Command Handler (`ApplyPatchCommand`) that is routed from the persistent TCP socket.
*   *Analysis:* Integrating a **Remote Desktop** utility is handled by registering a diagnostic background worker that spawns an isolated screen stream worker upon receiving a `StartScreenMirroring` signal. This completely bypasses the core state machine, preserving architecture integrity.

---

## PART 14: PRODUCTION READINESS AUDIT SCORE

Based on enterprise gaming requirements, we have evaluated Phase 1 across key operational metrics:

*   **Architecture (88%):** Clean separation of concerns with Session 0 service and Session 1 WPF shell. Strong dependency injection. Needs a formal State Machine.
*   **Reliability (75%):** Basic crash tracking is complete, but lacks robust local transaction isolation and database validation pipelines.
*   **Scalability (68%):** Real-time WMI queries are blocking and resource-intensive. Transition to asynchronous, throttled performance queries is mandatory.
*   **Maintainability (82%):** Clear modular assembly split. ViewModels are well-decoupled.
*   **Security (60%):** Weak shell lockdown. Plaintext configuration formats and lack of local certificate verification are significant vulnerabilities.
*   **Performance (70%):** Low-latency Named Pipes are excellent. Telemetry monitoring CPU utilization is too high on low-end hardware.
*   **Observability (80%):** Robust Serilog setup. Lacks MiniDump capture and structured OS event-log exporting.
*   **Extensibility (85%):** Good interface definitions and loose coupling via events.
*   **Windows Integration (55%):** Inadequate Job Object usage. Lack of native user session tokens and low-level keyboard hook implementations.
*   **Enterprise Readiness (62%):** High commercial risk due to inadequate shell locking, missing virtual disk integration, and weak offline billing counters.

### Overall Production Readiness Score: **70%**
*Verdict:* **Not Production Ready.** Phase 1 provides excellent local domain services, but requires deep Windows security API implementation, robust local transactions, and state machine transitions to meet enterprise deployment standards.

---

## PART 15: ENTERPRISE SPECIFICATION & ROADMAP

### 15.1 Missing Components
1.  `LowLevelKeyboardHook` (C++ Interop / PInvoke class to intercept and block OS hotkeys).
2.  `SecureDesktopSwitcher` (Win32 desktop isolation engine utilizing `CreateDesktop` API).
3.  `ProcessJobObjectAssigner` (Assigns launched game processes to a Windows Job Object structure).
4.  `WmiTelemetryThrottle` (Adaptive diagnostic poller throttling query intervals).
5.  `LocalTransactionVault` (Offline-resilient SQLite storage vault with monotonic signing).

### 15.2 Missing Services
1.  `IKioskSecurityService` (Controls Windows Shell overrides and enforces registry lockdown policies).
2.  `IWorkstationBackupService` (Performs DPAPI/AES-256 encrypted backups of configurations).
3.  `IPowerManagementService` (Triggers OS shutdown, restart, and logoff via Win32 shutdown commands).
4.  `IVirtualDiskService` (Mounts target iSCSI / remote network game directories dynamically).

### 15.3 Missing Interfaces
```csharp
public interface IKioskSecurityService
{
    Task EnforceLockdownAsync();
    Task ReleaseLockdownAsync();
    bool IsShortcutBlocked(int vkCode);
}

public interface IWorkstationBackupService
{
    Task<string> CreateBackupAsync(string targetDirectory, string destinationPath, string encryptionKey);
    Task<bool> RestoreBackupAsync(string backupPath, string extractPath, string decryptionKey);
}

public interface IPowerManagementService
{
    Task ShutdownAsync(bool force);
    Task RebootAsync(bool force);
    Task LogoffUserAsync();
}
```

### 15.4 Missing Background Workers
1.  `UiProcessWatchdogWorker` (Session 0 service monitoring Session 1+ interactive WPF process lifetime).
2.  `PnpDeviceWatcher` (Event-driven ACPI / USB arrival hardware hook monitor).
3.  `TelemetryAggregationWorker` (Buffers, decimes, and dispatches batch reports to minimize network load).

### 15.5 Missing State Machines
1.  `TerminalStateManager` (Stateless-backed explicit terminal state transition driver).
2.  `GameProcessStateMachine` (Tracks `IDLE` $\rightarrow$ `LAUNCHING` $\rightarrow$ `RUNNING` $\rightarrow$ `CRASH_RECOVERING` $\rightarrow$ `TERMINATED` states).

### 15.6 Missing Events
*   `KioskPolicyViolationDetectedEvent` (Fires when a user attempts registry modifications or shortcut bypasses).
*   `SystemThermalWarningEvent` (Fires when hardware telemetry detects temperatures exceeding safe thresholds).
*   `OfflineGracePeriodExpiredEvent` (Fires when disconnection grace timeout expires).
*   `JobObjectTerminatedEvent` (Fires when an entire game process tree is terminated).

### 15.7 Missing Contracts & Models
```csharp
public record KioskLockdownPolicy(
    bool DisableTaskManager,
    bool DisableRegistryTools,
    bool DisableCmdShell,
    string[] ProhibitedProcessNames
);

public record ThermalSensorReading(
    string SensorName,
    double CurrentTemperature,
    double CriticalThreshold,
    bool IsViolated
);

public record OfflineSessionState(
    string SessionId,
    string StationId,
    DateTime StartTime,
    double ElapsedSeconds,
    double AccumulatedCost,
    string HashSignature
);
```

### 15.8 Missing APIs
*   `POST /api/v1/workstations/{id}/telemetry/batch` (Compressed bulk metric reporting).
*   `POST /api/v1/workstations/{id}/alerts` (Critical security or thermal warning reporting).
*   `GET /api/v1/workstations/{id}/policies` (Retrieves latest Kiosk and Process policies).

### 15.9 Missing Configuration
```json
{
  "KioskSettings": {
    "LockShellOnStartup": true,
    "KeyboardHookEnabled": true,
    "DesktopIsolationEnabled": true
  },
  "TelemetryThrottle": {
    "ActivePollingIntervalMs": 2000,
    "IdlePollingIntervalMs": 30000,
    "BulkReportIntervalSeconds": 30
  },
  "OfflineResilience": {
    "GracePeriodMinutes": 5,
    "MaxOfflineSessionStorageMb": 500
  }
}
```

### 15.10 Recommended Folder Structure
```
Sayra.Client/
├── Sayra.Client.Core/             # Session 0 Windows Service (SCM)
│   ├── Infrastructure/
│   │   ├── WindowsServiceHost.cs
│   │   └── DesktopSessionManager.cs
│   ├── Security/
│   │   ├── KioskSecurityService.cs
│   │   └── KeyboardHookProvider.cs
│   ├── State/
│   │   └── TerminalStateMachine.cs
│   └── Workers/
│       ├── UiProcessWatchdogWorker.cs
│       └── PnpDeviceWatcher.cs
├── Sayra.Client.UI/               # Session 1+ WPF Shell Application
│   ├── App.xaml
│   └── Themes/
├── Sayra.Client.Shared/           # Contracts, Models, and Interfaces
│   ├── Interfaces/
│   ├── Models/
│   └── PInvoke/
│       ├── User32.cs
│       └── Kernel32.cs
└── Sayra.Client.Launcher/         # Game lifetime, Job Objects, and Sandboxing
    ├── ProcessJobObjectAssigner.cs
    └── SandboxVirtualizer.cs
```

### 15.11 Recommended Namespaces
*   `Sayra.Client.Core.Infrastructure`
*   `Sayra.Client.Core.Security`
*   `Sayra.Client.Core.State`
*   `Sayra.Client.Shared.PInvoke`
*   `Sayra.Client.Launcher.Sandboxing`

### 15.12 Dependency Graph
```
[Sayra.Client.UI (WPF Shell)]
       │
       ▼ (Named Pipe IPC)
[Sayra.Client.Core (Windows Service)]
       │
       ├─► [Sayra.Client.Shared (Common Models, PInvoke)]
       ├─► [Sayra.Client.Launcher (Job Objects, Execution Sandboxing)]
       ├─► [Sayra.Client.Diagnostics (Telemetry, Hardware Health)]
       └─► [Sayra.Client.Authentication (Credentials, Security Vault)]
```

### 15.13 Key Architectural Risks
1.  **Antivirus Interference with Hooking APIs:** Low-level keyboard hooks (`SetWindowsHookEx`) are often flagged or blocked by modern endpoint protection tools (Windows Defender, Kaspersky). Mitigate by utilizing the isolated Secure Desktop switching API as a primary defense.
2.  **WMI Execution Blockages:** WMI querying is notoriously slow and can freeze threads on corrupted Windows installations. Mitigate by utilizing raw Win32 APIs and wrapper libraries (e.g. OpenHardwareMonitor DLLs) with a strict execution timeout.
3.  **Local Database Corruption on Hard Power-Offs:** Hard shutdowns can corrupt active SQLite caches. Mitigate by enforcing SQLite Write-Ahead Logging (WAL) and backing up session tracking states in redundant configuration files.

### 15.14 Implementation Roadmap & Phase order
1.  **Phase 1A: Establish Session 0 Separation & IPC Channel:** Implement the Windows Service runner alongside the `CreateProcessAsUser` desktop spawning pipeline.
2.  **Phase 1B: Develop Low-Level Security Hooks & Desktop Switcher:** Implement low-level keyboard hooks and the isolated desktop switching APIs.
3.  **Phase 1C: Integrate Windows Job Objects:** Update the Process Manager to allocate all child processes to a Job Object container for clean terminations.
4.  **Phase 1D: Implement Monotonic Offline Billing Vault:** Implement the encrypted offline database utilizing processors' tick counters to protect session transactions.
5.  **Phase 1E: Optimize Telemetry and Hardware Poller:** Migrate the diagnostics poller to the asynchronous throttled pipeline to minimize CPU usage.
