# SAYRA Enterprise Windows Client — Phase 4: Game Runtime Management & Kiosk Control Specification

**Title:** Official Enterprise Runtime Management & Kiosk Control Architecture and Specification Document
**Version:** 4.0.0-RTM
**Author:** Principal Enterprise Windows Architect, Game Runtime Architect, and Technical Specification Writer
**Classification:** Proprietary / Enterprise Confidential
**Target Platform:** .NET 8, Windows Service (Session 0), WPF Shell (Session 1+), Windows 10/11 Workstations

---

## 1 Executive Summary

### 1.1 Purpose
The purpose of this specification is to define the authoritative, implementation-ready architecture, design patterns, Win32 API integrations, local system boundaries, security controls, and recovery protocols for **SAYRA Enterprise Windows Client (Phase 4 — Game Runtime Management & Kiosk Control)**. This document serves as the absolute, immutable **Single Source of Truth** for future implementation. It is structured with sufficient technical depth and low-level detail to allow any senior Windows system architect or advanced AI implementation agent to develop, compile, test, and deploy the entire subsystem with zero ambiguity and without requiring external clarifications.

### 1.2 Business Goals
*   **Prevent Station Escape and Tampering:** Ensure that under no circumstance can public users escape the designated gaming shell to access the host operating system, local files, or system administrative utilities, safeguarding cybercafe and gaming center hardware investments.
*   **Maximized Station Monetization:** Securely bind game execution, lifetime process states, and session timing with the central billing engine, ensuring that gameplay is terminated the instant an account balance or allotted session duration is depleted.
*   **Minimize Game Crash Latency & Downtime:** Automatically detect game, system, or launcher crashes and transparently recover the user session, game state, or shell locking overlays, preserving the user experience and preventing system-related session disruptions.
*   **Zero License Abuse:** Enforce strict verification of license tokens, digital signatures, and whitelisted process execution parameters, preventing unauthorized game launches or cheat utilities from running during active user sessions.

### 1.3 Technical Goals
*   **Advanced Kiosk Lockdown:** Replace the standard Windows Explorer shell with a secure, custom visual WPF shell, locked down via low-level Win32 keyboard/mouse hooks, separate custom Windows Desktops (`CreateDesktop`), and active registry group policies.
*   **Robust Game Process Supervision:** Track game launches, child process spawning trees, and runtime metrics using Windows Job Objects and custom ETW (Event Tracing for Windows) kernel process trace subscriptions, ensuring complete lifecycle tracking from start to exit.
*   **Immersive, Secure Direct3D Overlay:** Inject a secure visual overlay into active full-screen applications using DXGI swap chain wrappers and topmost WPF overlay strategies to display real-time timers, warnings, and messages without degrading gaming performance.
*   **Robust Sandboxing and Resource Controls:** Enforce process-level thread affinity and maximum memory footprints via Job Objects, optimizing workstation resource distribution between the active game, the visual shell, and background client tasks.

### 1.4 Kiosk Goals
*   Establish an absolute kiosk barrier. Standard Windows hotkeys (Alt+Tab, Win, Ctrl+Esc, Alt+F4, Ctrl+Alt+Del) must be blocked or captured before they reach the OS thread.
*   Restrict file access, command prompts (`cmd.exe`/`powershell.exe`), control panels, device managers, and modern Windows Settings apps using Windows Group Policy Registry overrides and active process-level blacklisting.

### 1.5 Runtime Goals
*   Coordinate multi-launcher support (Steam, Epic, Battle.net, Riot, GOG, EA, Custom) through unified launch profiles.
*   Monitor execution parameters, working directories, command-line arguments, environment variables, virtual registry entries, and custom file mappings dynamically.

### 1.6 Expected Outcomes
Upon full implementation of this specification, the SAYRA Enterprise Windows Client will deliver an un-bypassable kiosk container. Workstations will be capable of hosting competitive games with absolute process tracking, isolated secure desktop threads, low-overhead resource management, and automated crash self-healing. The client will remain resilient against hostile user environments, unauthorized USB insertions, and aggressive anti-cheat or overlay conflicts.

---

## 2 Phase Scope

### 2.1 Included (In-Scope Subsystems)
*   **Game Discovery Engine:** Deep local workstation scanning, multi-platform library discovery (Steam, Epic, Riot, GOG, Ubisoft, EA, Battle.net), metadata extraction, and library indexing.
*   **Game Launch Engine:** Command-line formatting, launch profiles, registry virtualization, file-system mapping, and sandbox directory configuration.
*   **Process Management:** Windows Job Object isolation, child-process tracking, CPU affinity mapping, RAM usage limits, process priority management, and zombie process cleanup.
*   **Session Enforcement:** Active billing session binding, playtime tracking, idle detection, forced logout, and automatic cleanup sequences.
*   **Runtime Overlay System:** Direct3D/DXGI-based in-game visual overlays, topmost WPF overlays, warning popups, real-time balance timers, and performance monitoring widgets.
*   **Game Protection Subsystem:** Whitelist/blacklist validation, cheat/debugger process detection, memory tampering monitors, DLL injection blockers, and code integrity audits.
*   **Runtime Recovery:** Crash detection, launcher recovery, state synchronization, and automatic shell/watchdog restoration.
*   **Shell Lockdown Engine:** Custom Windows Shell deployment, Explorer replacement, dedicated Win32 desktop creations (`SAYRA_SECURE_DESKTOP`), and shell restoration utilities.
*   **Keyboard & Mouse Restrictions:** Low-level global keyboard hooks (`WH_KEYBOARD_LL`), desktop containment clipping, mouse bounds locking, and raw input handling.
*   **System Restrictions:** Administrative blocking policies (Task Manager, CMD, PowerShell, Registry, Control Panel, Settings App, USB storage, Network configurations).
*   **Maintenance Mode:** Secure local administrator bypass authentication, policy suspension, automatic timeout relocks, and detailed audit trail logs.

### 2.2 Excluded
*   Developing ring-0 kernel-level anti-cheat drivers (relying instead on native Windows ring-3 APIs and ETW subscriptions).
*   Hardware BIOS configuration locks and network-level VLAN configurations (responsibility of third-party network deployment tools).
*   In-game microtransaction payment processing (billing is handled strictly on session duration and account balances).

### 2.3 Dependencies
*   **.NET 8 Runtime:** Execution host for both the Windows Service and the WPF Client Shell.
*   **Sayra.Client.Shared:** Shared contracts, IPC messages, and data entities.
*   **Sayra.Client.Guardian:** The independent diagnostic watchdog service designed to monitor the main Windows Service.
*   **Windows DPAPI & SQLCipher:** For secure, hardware-bound configuration and telemetry storage (as specified in Phase 3).
*   **DirectX / DXGI SDK:** For in-game overlay hooks and swap-chain presentation.

### 2.4 Success Criteria
1.  **Alt+Tab & Key Escape Block Rate:** 100% of standard OS escape hotkeys must be blocked at the keyboard driver hook level.
2.  **Job Object Tracking Accuracy:** 100% of game processes and child processes spawned by launchers (e.g. Steam web helpers) must be terminated instantly when the parent session expires.
3.  **WPF Overlay FPS Performance Impact:** Injecting overlays must consume $< 1\%$ of CPU cycles and decrease game frames-per-second (FPS) by less than $1\%$.
4.  **Crash Recovery Target:** Failed launcher processes must be fully recovered or restarted within 2,000 milliseconds.
5.  **Bypass Resistance:** It must be physically and logically impossible for a standard Windows user to spawn Task Manager, CMD, PowerShell, or Windows Settings while in Kiosk Mode.

### 2.5 Out of Scope
*   Managing central server database clusters.
*   Active Directory GPO server setups (all policies are applied locally via registry structures).

---

## 3 High-Level Architecture

```
                                            +--------------------------------------+
                                            |       SAYRA SERVER / ADMIN PORTAL    |
                                            +--------------------------------------+
                                                               ^
                                                               | TLS 1.3 Socket (Port 5000)
                                                               v
+-------------------------------------------------------------------------------------------------------------------------+
| WORKSTATION OPERATING SYSTEM BOUNDARY                                                                                   |
|                                                                                                                         |
|  +-------------------------------------------------------------------------------------------------------------------+  |
|  | SESSION 0 SYSTEM SPACE (High Privilege: NT AUTHORITY\SYSTEM)                                                       |  |
|  |                                                                                                                   |  |
|  |   +--------------------------+          +--------------------------+          +--------------------------------+  |  |
|  |   |    SAYRA WINDOWS SERVICE | <======> |    SECURE IPC PIPE       | <======> |      PROCESS SUPERVISOR        |  |  |
|  |   |    (Core Engine Host)    |          |   \\.\pipe\SayraIpcPipe  |          |   - Win32 Job Object Bindings  |  |  |
|  |   |    - Dynamic Sync        |          |   - SID Caller Validation|          |   - ETW Kernel Trace Monitors  |  |  |
|  |   |    - DPAPI Configuration |          |   - Strong DACLs         |          |   - Zombie Cleanup Engine      |  |  |
|  |   +--------------------------+          +--------------------------+          +--------------------------------+  |  |
|  |                 |                                                                             ^                   |  |
|  |                 | Spawns custom shell via WTS APIs                                            |                   |  |
|  |                 v                                                                             | Tracks processes  |  |
|  +----------------─┼─────────────────────────────────────────────────────────────────────────────┼───────────────────+  |
|                    |                                                                             |                      |
|  +─────────────────v─────────────────────────────────────────────────────────────────────────────┼───────────────────+  |
|  | SESSION 1+ USER INTERACTIVE SPACE (Low Privilege: Interactive Gamer Account)                  |                   |  |
|  |                                                                                               |                   |  |
|  |   +-------------------------------------------------------------------------------------------┼----------------+  |  |
|  |   |   SAYRA SECURE DESKTOP (Created via native Win32 CreateDesktop API)                           |                |  |  |
|  |   |                                                                                           |                |  |  |
|  |   |   +---------------------------------------+       +---------------------------------------+                |  |  |
|  |   |   |        SAYRA WPF KIOSK SHELL          |       |       LAUNCHED GAME PROCESS TREE       |                |  |  |
|  |   |   |   - Global Keyboard Hook (WH_LL_KB)   |       |   - Assigned CPU Thread Affinity       |                |  |  |
|  |   |   - Low-Level Mouse Confiner Window   |       |   - Memory Cap Enforced via Job Object|                |  |  |
|  |   |   - Dynamic Arabic/Farsi/English UI   |       |   - Protected from User Alteration     |                |  |  |
|  |   |   +---------------------------------------+       +---------------------------------------+                |  |  |
|  |   |                       |                                                       ^                        |  |  |
|  |   |                       | Renders real-time timers and messages                 | Direct3D DXGI Hook     |  |  |
|  |   |                       v                                                       |                        |  |  |
|  |   |   +---------------------------------------------------------------------------┼────────────────────+   |  |  |
|  |   |   |                             RUNTIME OVERLAY WINDOW                        │                    |   |  |  |
|  |   |   |   - DirectX/DXGI Present SwapChain Wrapper (In-Game Overlay Overlay)      v                    |   |  |  |
|  |   |   |   - Topmost Transparent Layer (Fallback Windowed Overlay System)                               |   |  |  |
|  |   |   +------------------------------------------------------------------------------------------------+   |  |  |
|  |   +------------------------------------------------------------------------------------------------------------+  |  |
|  +-------------------------------------------------------------------------------------------------------------------+  |
+---------------------------------------------------------------------------------------------------------+
```

### 3.1 Process Hierarchy & Spawning Architecture
All process spawning operations must bypass the standard shell commands to avoid path hijacking or privilege escalation:
1.  **Session 0 Service** initiates game launching by checking active license states and preparing configurations.
2.  The Service calls `CreateProcessAsUser` using the interactive user's environment token (Session 1+).
3.  The spawned process is immediately assigned to an active, secure **Windows Job Object** managed by the Session 0 Process Supervisor.
4.  If a launcher (e.g. Steam) is started, any child process spawned by that launcher is automatically swept into the same Job Object container by the Windows kernel.

### 3.2 Session Isolation (0 vs 1+ Boundaries)
*   **Session 0 (Windows Service):** Runs in a non-interactive workspace with high system privileges (`NT AUTHORITY\SYSTEM`). It cannot display UI elements. It manages file system access, processes, registry configurations, and ETW trace monitors.
*   **Session 1+ (Interactive WPF Shell):** Runs as a standard, non-privileged user. Displays the game library, handles keyboard/mouse input restrictions, and presents notifications.
*   **Bridging Channel:** Communication is handled exclusively over a secure local Named Pipe IPC server, preventing command spoofing or unauthorized token delegation.

### 3.3 Privilege Boundaries & Access Controls
*   The interactive user account running on the gaming terminal **must be a standard restricted user**, never a local administrator.
*   All administrative overrides (e.g., driver updates or OS restarts) are requested via IPC to Session 0. Session 0 validates the credentials against PBKDF2 hash blocks before executing the command.

### 3.4 Watchdog and Guardian Architecture
*   **`Sayra.Client.Guardian`:** A separate, lightweight Windows Service that runs in the background. It monitors the status of the main Windows Service (`Sayra Client`). If the main service is killed or crashes, the Guardian immediately restarts it, restores active Job Objects, and maintains the locked workstation state.
*   **Internal Service Watchdog:** The main service actively monitors the health of the WPF shell in Session 1+ over Named Pipe heartbeats. If the WPF process terminates unexpectedly, the service spawns a new instance of the visual shell on the secure desktop within 500ms.

### 3.5 Overlay Presentation Architecture
*   **In-Game Overlay:** Injects a lightweight DXGI swap chain presenter hook (`d3d11.dll`/`d3d12.dll`) to render session timers and warning messages directly inside full-screen games.
*   **WPF Fallback Overlay:** If DirectX hooks are blocked by game anti-cheat engines (e.g. Easy Anti-Cheat), the system falls back to spawning a transparent, topmost borderless WPF window configured with `WS_EX_TRANSPARENT` and `WS_EX_NOACTIVATE` styles to prevent stealing focus.

---

## 4 Game Runtime Components

### 4.1 Game Discovery Engine

The Game Discovery Engine automatically scans workstation storage volumes to discover installed digital games and register them to the local library.

#### 4.1.1 Scanning Architecture
*   The Discovery Engine scans registered gaming platform installations and recursively searches local drives, utilizing path exclusion heuristics to skip OS-critical folders (`C:\Windows`, `C:\ProgramData\Microsoft`, `AppData`, etc.) to minimize disk IO overhead.

#### 4.1.2 Platform Detectors
*   **Steam Detector:** Resolves Steam path via Registry keys `HKLM\SOFTWARE\WOW6432Node\Valve\Steam` (value `InstallPath`). It parses all defined `libraryfolders.vdf` files to scan across multiple hard disk drives. It then parses local `appmanifest_*.acf` files to extract precise AppIDs, Name strings, and installation directories.
*   **Epic Games Detector:** Parses all manifest files (`*.item`) located under `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\`. It extracts the `MandatoryAppFolderName`, `LaunchExecutable`, and `AppName` variables.
*   **Battle.net Detector:** Scans the `.build.info` files and registry locations under `HKLM\SOFTWARE\WOW6432Node\Blizzard Entertainment` to identify installed games and launchers.
*   **Riot Games Detector:** Parses `%PROGRAMDATA%\Riot Games\RiotClientInstalls.json` configuration blocks to locate active Riot Games launchers and active game installation metadata.
*   **Custom Launcher Detector:** Scans specified custom directories against signature manifests, parsing product properties and executable executable headers.

#### 4.1.3 Metadata and PE Extraction
*   The engine extracts details from found files including: File Version Info blocks (Product Name, Version, Copyright, Company), SHA-256 hash checksums, and application icons.
*   **Executable Validation:** Before indexing, the scanner reads the first two bytes of files to verify the `MZ` executable signature header (`0x5A4D`), dropping any spoofed or non-runnable files.

---

### 4.2 Game Launch Engine

The Game Launch Engine configures the operational environment and launches games securely under runtime constraints.

#### 4.2.1 Launch Profiles & Virtualization
*   **Launch Profiles:** Each game is bound to a `LaunchProfile` specifying executable paths, mandatory arguments, working directories, registry virtualization maps, and sandbox configurations.
*   **Registry Virtualization:** Prior to launch, any required game registry keys are constructed under temporary user-context branches or virtualized to prevent games from altering system-level parameters.
*   **File Mapping:** Maps game directories using directory symbolic links (`mklink`) or junction points when directory redirection is needed for legacy games.
*   **Launch Timeout Handling:** Monitors the launch sequence. If a game fails to create an active window handle within a configurable timeout (e.g., 30 seconds), the engine terminates the launch sequence, frees the license token, and reports a launch failure to the billing engine.

---

### 4.3 Process Management

This component enforces complete lifecycle tracking and system isolation on active processes.

```
       [Service Spawns Game]
                 │
                 ▼
     [Create Win32 Job Object]
                 │
                 ▼
  [Apply JO_LIMIT_KILL_ON_JOB_CLOSE]
                 │
                 ▼
   [Apply Cpu Thread Affinity Maps]
                 │
                 ▼
    [Set Job Memory Limit Caps]
                 │
                 ▼
 [Monitor Child Spawning over Job Objects]
                 │
   ┌─────────────┴─────────────┐
   ▼ (Active Session)          ▼ (Session Expiration)
[Monitor CPU/RAM Metrics]   [Close Job Object Handle]
                               │
                               ▼
                    [Kernel Automatically Kills]
                    [Every Spawned Process in Job]
```

#### 4.3.1 Windows Job Objects Integration
*   **Isolation Boundaries:** Every spawned game is placed inside a dedicated Win32 Job Object container.
*   **Settings Flags:** Enforces `JO_LIMIT_KILL_ON_JOB_CLOSE` configurations. When the parent handle inside the Windows Service is closed, the Windows kernel terminates all associated processes automatically.
*   **Zombie Process Cleanup:** When games close, any orphaned launcher helper processes (e.g. `SteamWebHelper.exe` or update checking scripts) are caught by the Job Object and terminated, preventing memory leaks on workstations.

#### 4.3.2 Resource Allocation and Thread Affinity
*   **Affinity Mapping:** Assigns specific CPU core masks dynamically to running games (e.g., cores 2-7, leaving cores 0-1 dedicated to the custom locked shell UI and background Windows Service).
*   **Priority Management:** Configures process scheduling priorities (`HIGH_PRIORITY_CLASS` during gameplay, dropping back to `IDLE_PRIORITY_CLASS` if the game is minimized or inactive).
*   **Memory Caps:** Enforces hard physical memory limits on the Job Object to prevent memory leak crashes from starving the operating system.

---

### 4.4 Session Enforcement

Enforces the relationship between user balances, active sessions, and game execution.

#### 4.4.1 Playtime & Idle Detection
*   **Session Binding:** A game process tree is bound to an active `RuntimeSession`. If no valid player session exists, game execution is blocked.
*   **Playtime Tracking:** Tracks precise elapsed game durations.
*   **Idle Detection:** Monitors user input events using Win32 raw input APIs. If mouse and keyboard inputs are absent for a defined threshold (e.g., 10 minutes) during an active session, the system pops up a localized visual warning. If ignored, it pauses the session or logs out the user.
*   **Forced Logout and Grace Period:** When a user's session time is depleted:
    1.  The UI displays warning overlays at 5-minute, 2-minute, and 30-second marks.
    2.  At 0 seconds, a 15-second grace period starts.
    3.  If no extension is purchased, the system closes the game's Job Object, runs the file cleanup engine (deleting temp saves and caches), and logs out the interactive user.

---

### 4.5 Runtime Overlay System

The Runtime Overlay System displays critical session data, timers, and messages over game screens.

#### 4.5.1 Rendering Strategies
*   **DXGI SwapChain Present Hook:** Utilizes a native C++ hook wrapper targeting DirectX 11/12 `IDXGISwapChain::Present`. It injects a custom, high-performance visual widget into the graphics pipelines of running games.
*   **Performance Overlay:** Displays real-time game FPS, active GPU temperatures, system CPU utilization, and local system network latency metrics.
*   **Overlay Security:** The overlay contains no interactive keyboard or mouse inputs during active gameplay to prevent user interaction exploits.

---

### 4.6 Game Protection

Protects the gaming terminal and game executables from unauthorized modifications, cheats, or hacking attempts.

#### 4.6.1 Tamper Prevention Policies
*   **Unauthorized Executable Detection:** Actively scans the operating system's process space using ETW process trace event subscriptions. Spawning any executable not matching the whitelisted paths of registered games or system files triggers immediate process termination.
*   **Cheat and Debugger Blockers:** Checks for running debugger processes (`IsDebuggerPresent` checks and driver handle tests). It monitors memory injection routines (`WriteProcessMemory`/`CreateRemoteThread` system API hooks) and blocks unauthorized DLL loading operations.
*   **Whitelist Verification:** Validates that game binaries have valid code signatures.

---

### 4.7 Runtime Recovery

The Runtime Recovery component ensures system stability and continuity during runtime failures.

#### 4.7.1 Crash & State Reconciliation
*   **Crash Recovery:** If a game terminates with a non-zero exit code or runs for less than 5 seconds before closing, the recovery engine marks it as crashed, increments the crash counter, and restarts it (up to 3 times before displaying a diagnostic alert).
*   **State Reconciliation:** Periodically compares the workstation's local session state with the central server's logs. If a network disconnect occurs and recovers, the client synchronizes its states with the server, recovering any running game processes.

---

## 5 Kiosk Control Components

### 5.1 Shell Lockdown

The Shell Lockdown system replaces the traditional Windows Explorer interface with a locked down kiosk shell.

#### 5.1.1 Custom Shell Integration
*   The default shell registry key `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell` is set to point directly to `Sayra.UI.exe` instead of `explorer.exe` during active gaming operations.
*   **Secure Desktop Strategy:** Uses the Win32 `CreateDesktop` API to create an isolated desktop space named `SAYRA_SECURE_DESKTOP`. The custom visual shell is executed within this isolated desktop context. Traditional windows shortcuts, helper utilities, and menus do not exist in this secure workspace.
*   **Shell Restoration:** Upon transitioning into maintenance mode, the system safely restores `explorer.exe` to the shell registry and switches the desktop context back to the default desktop thread.

---

### 5.2 Keyboard Restrictions

Prevents users from bypassing Kiosk Mode using keyboard shortcuts.

#### 5.2.1 Low-Level Keyboard Hooks
*   Registers a global low-level keyboard hook (`WH_KEYBOARD_LL`) targeting `user32.dll`. It intercepts keystrokes before they reach target windows.
*   **Blocked Keyboard Combinations:**
    *   `Alt+Tab` (Task Switching)
    *   `WinKey` (Start Menu / Search)
    *   `Win+R` (Run Dialog)
    *   `Ctrl+Esc` (Start Menu)
    *   `Alt+F4` (App Termination)
    *   `Ctrl+Alt+Tab` / `Alt+Shift+Tab` (Task Switching UI)
*   **Secure Desktop Fallback:** Since `Ctrl+Alt+Del` cannot be blocked by standard Win32 hooks due to Windows security architecture, the system relies on GPO registry keys (`Software\Microsoft\Windows\CurrentVersion\Policies\System\DisableTaskMgr`) to disable Task Manager execution when `Ctrl+Alt+Del` is pressed.

---

### 5.3 Mouse Restrictions

Restricts mouse pointer operations to designated screen workspaces.

#### 5.3.1 Confinement clipping
*   **Confinement clipping:** Restricts mouse movement boundaries using the Win32 `ClipCursor` API to prevent users from interacting with background windows on secondary monitors during active gameplay.
*   **Edge Lock:** Forces the mouse pointer to remain within the active game window or locked shell screen bounds.
*   **Pointer Visibility Control:** Dynamically hides or shows the system mouse pointer using native `ShowCursor` handles when requested by games or custom overlay elements.

---

### 5.4 System Restrictions

Implements strict system-level policies to prevent unauthorized administrative actions.

#### 5.4.1 Registry and Process Blocks
The following system features are locked down via local registry group policy overrides:
*   **Task Manager:** Enforced via `DisableTaskMgr = 1` registry policies.
*   **CMD / PowerShell:** Blocks processes matching `cmd.exe` or `powershell.exe` from executing.
*   **Control Panel / Settings:** Disables Access to Control Panel (`NoControlPanel = 1`).
*   **USB Restrictions:** Monitors USB insertions using `WM_DEVICECHANGE` alerts. Any storage device (`USBSTOR`) is automatically blocked or unmounted to prevent players from loading malware or executing unauthorized scripts.
*   **Network Adapter Restrictions:** Disables network adapter configurations, preventing users from altering IP addresses, DNS settings, or MAC addresses.

---

### 5.5 Maintenance Mode

Enables authorized administrators to unlock the terminal for system updates or hardware maintenance.

#### 5.5.1 Administrator Bypass Validation
*   **Local Admin Bypass:** The visual shell contains a hidden administrative login window (triggered via `Ctrl+Alt+Shift+M` or dynamic sequence keys).
*   **Authentication:** The administrator inputs local credentials. The service validates the credentials using secure PBKDF2 hash blocks.
*   **Automatic Relock:** Upon entering Maintenance Mode, the kiosk restrictions are temporarily suspended. If no administrative input is detected for a configurable timeout (e.g., 20 minutes), the client automatically re-locks the system, re-enables keyboard/mouse hooks, and restores the custom shell.
*   **Audit Logging:** Every transition into or out of Maintenance Mode is logged to the local encrypted SQLite audit database and sent to the central server.

---

## 6 Domain Models

This section provides complete C# class declarations for all domain models inside the `Sayra.Client.Shared.Models` namespace.

```csharp
using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models
{
    /// <summary>
    /// Represents the full metadata and scanning properties of a discovered game.
    /// </summary>
    public class GameProfile
    {
        public string GameId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string InstallationDir { get; set; } = string.Empty;
        public string PlatformSource { get; set; } = "Scanner"; // Steam, Epic, Battle.net, Custom
        public string Version { get; set; } = "1.0.0";
        public string Sha256Hash { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public bool IsWhitelisted { get; set; } = true;
        public string Base64Icon { get; set; } = string.Empty;
        public DateTime LastScanned { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Defines command-line arguments, registry maps, and environment parameters for launching a game.
    /// </summary>
    public class LaunchProfile
    {
        public string GameId { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public bool RunAsAdministrator { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
        public Dictionary<string, string> VirtualRegistryKeys { get; set; } = new();
        public string SandboxPath { get; set; } = string.Empty;
        public int LaunchTimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Tracks the active user billing session, timers, and active game links.
    /// </summary>
    public class RuntimeSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ActiveGameId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime EndTime { get; set; }
        public double Balance { get; set; }
        public TimeSpan TimeRemaining { get; set; }
        public string SessionState { get; set; } = "Active"; // Active, Paused, GracePeriod, Expired
        public bool IsOfflineSession { get; set; }
    }

    /// <summary>
    /// Maps the process execution tree representing game and launcher subprocesses.
    /// </summary>
    public class ProcessTree
    {
        public int ParentProcessId { get; set; }
        public string ParentName { get; set; } = string.Empty;
        public List<int> ChildProcessIds { get; set; } = new();
        public List<string> ChildNames { get; set; } = new();
        public string JobObjectName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Holds visual states, resolution details, and layout settings for the active overlay.
    /// </summary>
    public class OverlayState
    {
        public bool IsOverlayActive { get; set; }
        public string ActiveLayout { get; set; } = "Compact"; // Compact, Expanded, Diagnostic
        public double Opacity { get; set; } = 0.85;
        public int FrameRate { get; set; }
        public double CpuUsagePercentage { get; set; }
        public double RamUsageMb { get; set; }
        public string NetworkLatencyMs { get; set; } = "0ms";
    }

    /// <summary>
    /// Defines active lockdown rules, allowed files, and blocked hotkey policies.
    /// </summary>
    public class KioskPolicy
    {
        public bool EnableKioskLockdown { get; set; } = true;
        public bool BlockKeyboardHooks { get; set; } = true;
        public bool ClipMouseBounds { get; set; } = true;
        public bool BlockTaskManager { get; set; } = true;
        public bool BlockUsbStorage { get; set; } = true;
        public bool BlockCommandPrompts { get; set; } = true;
        public List<string> BlacklistedProcessNames { get; set; } = new();
        public List<string> WhitelistedProcessNames { get; set; } = new();
    }

    /// <summary>
    /// Manages properties, durations, and administrative validations for maintenance mode.
    /// </summary>
    public class MaintenanceSession
    {
        public string MaintenanceId { get; set; } = string.Empty;
        public string AdministratorUsername { get; set; } = string.Empty;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public int AutoLockTimeoutMinutes { get; set; } = 20;
        public string AuditReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Captures the runtime integrity state of game executables and directories.
    /// </summary>
    public class GameIntegrityState
    {
        public string GameId { get; set; } = string.Empty;
        public bool IsSignatureValid { get; set; }
        public bool DirectoryTampered { get; set; }
        public string LastError { get; set; } = string.Empty;
        public DateTime VerificationTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Structured diagnostic telemetry model representing process failures.
    /// </summary>
    public class CrashReport
    {
        public string CrashId { get; set; } = string.Empty;
        public string GameId { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public int ExitCode { get; set; }
        public double ExecutionDurationSeconds { get; set; }
        public string CrashDumpPath { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Real-time hardware utilization metrics snapshot.
    /// </summary>
    public class RuntimeResourceSnapshot
    {
        public double CpuUtilizationPercentage { get; set; }
        public double AllocatedRamMb { get; set; }
        public double GpuUtilizationPercentage { get; set; }
        public double GpuVramMb { get; set; }
        public double GpuTemperatureCelsius { get; set; }
        public DateTime SampleTimestamp { get; set; } = DateTime.UtcNow;
    }
}
```

---

## 7 Interfaces

Below are the complete public interface declarations for Phase 4, defined in the `Sayra.Client.Shared.Interfaces` namespace.

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    /// <summary>
    /// Discovers, validates, and indexes installed games across local storage volumes.
    /// </summary>
    public interface IGameDiscoveryService
    {
        Task<IEnumerable<GameProfile>> ScanStorageVolumesAsync(CancellationToken cancellationToken);
        Task<GameProfile?> DetectSteamLibraryAsync(CancellationToken cancellationToken);
        Task<GameProfile?> DetectEpicGamesLibraryAsync(CancellationToken cancellationToken);
        Task<GameProfile?> DetectBattleNetLibraryAsync(CancellationToken cancellationToken);
        Task<GameProfile?> DetectRiotGamesLibraryAsync(CancellationToken cancellationToken);
        Task<GameProfile?> DetectCustomLauncherAsync(string path, CancellationToken cancellationToken);
        Task<bool> ValidateExecutableHeaderAsync(string exePath);
    }

    /// <summary>
    /// Orchestrates game launching sequences, parameter bindings, environment setups, and registry mapping.
    /// </summary>
    public interface IGameLaunchService
    {
        Task<bool> LaunchGameAsync(string gameId, LaunchProfile profile, CancellationToken cancellationToken);
        Task<bool> PrepareLaunchEnvironmentAsync(LaunchProfile profile);
        Task<bool> ApplyRegistryVirtualizationAsync(string gameId, Dictionary<string, string> registryKeys);
        Task<bool> SetupFileMappingsAsync(string gameId, string sandboxPath);
        Task<bool> HandleLaunchTimeoutAsync(string gameId, int timeoutSeconds);
    }

    /// <summary>
    /// Supervises running processes and child processes, maps CPU affinity, sets RAM limits, and handles process termination.
    /// </summary>
    public interface IProcessSupervisor
    {
        event Action<int, string> ProcessSpawned;
        event Action<int, int> ProcessTerminated; // PID, Exit Code

        Task<bool> BindToJobObjectAsync(int processId, string gameId);
        Task<bool> SetThreadAffinityAsync(int processId, ulong cpuAffinityMask);
        Task<bool> LimitMemoryAllocationAsync(string jobObjectName, long maxMemoryBytes);
        Task<ProcessTree> GetProcessTreeAsync(string gameId);
        Task TerminateProcessTreeAsync(string gameId, bool force);
        Task CleanupZombieProcessesAsync();
    }

    /// <summary>
    /// Enforces the custom visual shell, switches native Windows Desktops, and manages shell restoration transitions.
    /// </summary>
    public interface IKioskSecurityService
    {
        Task ApplyCustomShellRegistryAsync();
        Task SpawnSecureDesktopAsync(string desktopName);
        Task SwitchToSecureDesktopAsync();
        Task RestoreStandardExplorerShellAsync();
        bool IsSecureDesktopActive();
    }

    /// <summary>
    /// Injectively overlays timers, warning panels, and performance telemetry directly onto full-screen game contexts.
    /// </summary>
    public interface IOverlayService
    {
        Task InitializeDxgiPresentHookAsync();
        Task RenderTimerOverlayAsync(TimeSpan timeRemaining, double balance);
        Task DisplayWarningPopupAsync(string message, int durationSeconds);
        Task DisplayAdminMessageAsync(string header, string content);
        Task RenderPerformanceStatsAsync(RuntimeResourceSnapshot statistics);
        Task DestructOverlayAsync();
    }

    /// <summary>
    /// Enforces runtime session constraints, calculates playtime, executes idle checks, and coordinates forced logouts.
    /// </summary>
    public interface ISessionEnforcementService
    {
        event Action<string> SessionExpired;
        event Action<string> IdleTimeoutTriggered;

        Task BindActiveSessionAsync(RuntimeSession session);
        Task StartPlaytimeTrackingAsync(string sessionId);
        Task UpdateActiveBalanceAsync(double newBalance);
        Task<bool> EvaluateIdleDetectionAsync();
        Task ExecuteForcedLogoutSequenceAsync(string sessionId);
    }

    /// <summary>
    /// Validates executables, monitors memory regions for hooks, checks whitelists, and blocks cheat engines.
    /// </summary>
    public interface IGameIntegrityService
    {
        Task<bool> PerformExecutableHashValidationAsync(string exePath, string expectedHash);
        Task<bool> StartMemoryTamperMonitoringAsync(int processId);
        Task<bool> BlockDllInjectionsAsync(int processId);
        Task<bool> RegisterRuntimeProcessWhitelistAsync(IEnumerable<string> whitelistedNames);
        Task<bool> EnforceAntiLaunchBypassAsync();
    }

    /// <summary>
    /// Orchestrates crash self-healing, state reconciliation, and watchdog recovery services.
    /// </summary>
    public interface IRuntimeRecoveryService
    {
        Task HandleProcessCrashAsync(CrashReport report);
        Task ReconcileWorkstationStateAsync(RuntimeSession serverVerifiedSession);
        Task RestartWatchdogMonitoringAsync();
        Task PerformEmergencyLockdownAsync(string reason);
    }

    /// <summary>
    /// Installs and manages low-level global system input hooks to trap keyboard escape commands and mouse bounds.
    /// </summary>
    public interface IInputRestrictionService
    {
        void InstallKeyboardHooks();
        void UninstallKeyboardHooks();
        void ClipMouseBoundsToWindow(IntPtr windowHandle);
        void ReleaseMouseBounds();
        void ControlPointerVisibility(bool visible);
    }

    /// <summary>
    /// Manages secure administrative bypass, policy temporary relaxation, and session re-lock timer timeouts.
    /// </summary>
    public interface IMaintenanceModeService
    {
        Task<bool> ChallengeAdministratorCredentialsAsync(string username, string encryptedPassword);
        Task SuspendKioskRestrictionsAsync(MaintenanceSession session);
        Task RegisterActivityTickAsync();
        Task TerminateMaintenanceModeAsync();
    }
}
```

---

## 8 Data Flow

### 8.1 Game Discovery Flow
```
[Background Timer Trigger]
       │
       ▼
[IGameDiscoveryService.ScanStorageVolumesAsync]
       │
       ├──► Parse Registry Keys (Steam, GOG, Epic, Ubisoft, EA)
       ├──► Parse Manifest Files (Steam VDF, Epic JSON, Riot JSON)
       └──► Crawl Custom Directories (excluding system paths)
       │
       ▼
[Verify Executables] ──► Validate MZ Magic Header (0x5A4D)
       │
       ▼
[Extract Metadata] ──► Query FileVersionInfo, SHA256 checksums, base64 icons
       │
       ▼
[Cache Check] ──► Match FileSize & LastModifiedTime to cache file to skip parsing
       │
       ▼
[Register Games] ──► Add discovered GameProfiles to local database
```

### 8.2 Game Launch & Session Integration Flow
```
WPF Client UI           IpcPipeGateway         SessionService          ProcessSupervisor
    │                         │                       │                        │
    │─── LAUNCH_GAME ────────►│                       │                        │
    │    (GameId)             │─── VerifySession ────►│                        │
    │                         │    (Active & Balance) │                        │
    │                         │◄── Session OK ────────│                        │
    │                         │                       │                        │
    │                         │─── ExecuteLaunch ────►│                        │
    │                         │    (IGameLaunchService)│                       │
    │                         │                       │─── CreateProcessAsUser │
    │                         │                       │    (Session 1+ Token)  │
    │                         │                       │◄── Process Spawning PID│
    │                         │                       │                        │
    │                         │─── TrackProcess ──────────────────────────────►│ (Create Job Object)
    │                         │                                                │ (Assign Cpu Core Mask)
    │                         │                                                │ (Limit RAM Bounds)
    │                         │◄── Spawning Tracked ───────────────────────────│
    │                         │                       │                        │
    │                         │─── ActivateOverlay ──►│                        │ (Inject DXGI SwapChain)
    │◄── START_SUCCESS ───────│                       │                        │
```

### 8.3 Forced Logout Flow
```
[Session Balance Depleted] ──► [ISessionEnforcementService Triggered]
                                                │
                                                ▼
                                [Overlay Warning Popups Displayed]
                                 (5min -> 2min -> 30sec -> 0sec)
                                                │
                                                ▼
                                    [15-Second Grace Period]
                                                │
                               ┌────────────────┴────────────────┐
                               ▼ (Extended)                      ▼ (Expired)
                     [Resume Normal Play]           [Execute Forced Logout Sequence]
                                                                 │
                                                                 ▼
                                                  [ProcessSupervisor.TerminateProcessTree]
                                                   (Closes Job Object -> Auto Kills Tree)
                                                                 │
                                                                 ▼
                                                    [Run Environment Cleanup]
                                                    (Delete temp files and caches)
                                                                 │
                                                                 ▼
                                                  [WpfShell displays Login Screen]
                                                  [Re-enable absolute desktop locks]
```

### 8.4 Unauthorized Process Detection Flow
```
[ETW Kernel Process Monitor] ──► Intercepts new Process Creation Event
                                                │
                                                ▼
                               [Query Process Path & Code Signature]
                                                │
                               ┌────────────────┴────────────────┐
                               ▼ (Whitelisted/Valid Signature)   ▼ (Not Whitelisted/Modified)
                         [Allow Run]                    [Trigger Security Alert]
                                                                 │
                                                                 ▼
                                                  [ProcessSupervisor.TerminateProcess]
                                                  (Terminate PID instantly)
                                                                 │
                                                                 ▼
                                                  [Write to SQLCipher Security Audit]
                                                  [Dispatch alert over UDP/TCP to Admin]
```

### 8.5 Maintenance Mode Validation Flow
```
[Ctrl+Alt+Shift+M Pressed] ──► [WPF Shell displays login dialog]
                                                │
                                                ▼
                                [Input Admin Credentials]
                                                │
                                                ▼
                               [Secure Named Pipe IPC to Session 0]
                                                │
                                                ▼
                               [Validate credentials against PBKDF2]
                                                │
                               ┌────────────────┴────────────────┐
                               ▼ (Invalid Credentials)           ▼ (Authorized Bypass)
                         [Log Failed Attempt]           [IMaintenanceModeService.Suspend]
                         [Increment Lockout Timer]               │
                                                                 ▼
                                                  [Suspend Global Keyboard/Mouse Hooks]
                                                  [Restore explorer.exe shell]
                                                  [Start Auto-Relock Timer (20 mins)]
```

---

## 9 Threading Model

### 9.1 Background Workers
All background tasks (polling, telemetry collection, file crawling, and database synchronization) run exclusively on background thread pool threads. This is implemented by inheriting from .NET's `BackgroundService` base class.

### 9.2 Overlay UI Thread
The visual overlay renders inside the active game's graphics context. The DXGI Present hooks run inside the game process's rendering threads. The fallback topmost WPF transparent overlay window runs on its own dedicated visual thread inside the WPF Shell process (`Sayra.UI.exe`). This thread is configured with high-priority execution parameters to ensure consistent rendering performance.

### 9.3 Process Monitoring Thread
Process monitoring utilizes a single, non-blocking background thread that processes event streams from the native Windows kernel via Event Tracing for Windows (ETW). This thread publishes process creation and termination notifications to our internal event aggregator, eliminating the need for periodic process scanning loops.

### 9.4 Input Hook Thread
Low-level Win32 hooks (`SetWindowsHookEx`) require an active Windows message loop (`GetMessage`/`TranslateMessage`/`DispatchMessage`) to intercept events correctly. The `InputRestrictionService` executes this message loop on a dedicated thread in the WPF client shell process to avoid blocking UI rendering or input handling.

### 9.5 Watchdog Thread
The Watchdog Thread runs inside the `Sayra.Client.Guardian` process. It executes an isolated wait loop monitoring the process handle of the primary Windows Service, utilizing the Win32 `WaitForSingleObject` API to instantly detect service termination.

### 9.6 Synchronization & Concurrency Strategy
*   **State Locking:** Enforces the use of `SemaphoreSlim(1, 1)` for asynchronous resource locking, preventing resource deadlocks on workstation execution pools.
*   **Thread-Safe Caching:** Shared state collections (e.g., active process pools) are stored in thread-safe collections from `System.Collections.Concurrent`.
*   **Volatile Pointers:** Global state properties are marked `volatile` to support atomic memory swapping during configuration updates.

### 9.7 Cancellation and Deadlock Prevention
*   Every asynchronous call is bound to a passed `CancellationToken`, supporting graceful thread cancellation during workstation shutdowns.
*   Blocking calls like `.Result` and `.Wait()` are strictly prohibited to prevent thread starvation.
*   Calls to external dependencies utilize `.ConfigureAwait(false)` to bypass UI synchronization context capturing.

---

## 10 Reliability

### 10.1 Crash Recovery
If a game terminates with a non-zero exit code or runs for less than 5 seconds before closing, the recovery engine marks it as crashed, increments the crash counter, and restarts it (up to 3 times before displaying a diagnostic alert).

### 10.2 Power Failure Resilience
The workstation state is synchronized to the encrypted local SQLite database before applying state changes. If a power failure occurs, the background service restores the last saved state from the database upon reboot, resuming active user sessions or maintaining system locks.

### 10.3 Explorer Crash Recovery
If the standard Windows Explorer shell is used and crashes during Maintenance Mode, the `KioskSecurityService` intercepts the crash and restarts `explorer.exe` to restore the administrative interface.

### 10.4 Keyboard Hook Failure Recovery
The custom locked shell UI runs a periodic 5-second check to verify that the keyboard hook handles are active and responding. If hook degradation is detected, the service uninstalls the current hooks, registers new global hook handles, and logs an event.

### 10.5 Overlay Crash Recovery
If the Direct3D DXGI Present hook crashes, the hook engine catches the exception, unbinds from the game process, and falls back to using the WPF transparent overlay window fallback system within 500ms to preserve session UI visibility.

### 10.6 Game Freeze and Infinite Loading Detection
*   **Freeze Detection:** Monitors game process window responsiveness using `SendMessageTimeout` with `WM_NULL` values. If a window fails to respond for more than 15 seconds during active gameplay, it is flagged as frozen.
*   **Infinite Loading Detection:** If a game remains in a high-CPU or high-disk IO state with zero user input events for more than 5 minutes during startup, the system prompts the user. If unresolved, it terminates the process and releases session locks.

---

## 11 Security

### 11.1 Kiosk Bypass Prevention
Standard Windows shell escapes are blocked by executing the WPF client shell within a custom Win32 desktop environment (`CreateDesktop` APIs), preventing access to the traditional explorer taskbar, start menu, or standard shortcut processors.

### 11.2 Secure Desktop
The `CreateDesktop` API is used to spin an isolated desktop named `SAYRA_SECURE_DESKTOP`. The custom visual shell is executed within this isolated desktop context. Traditional windows shortcuts, helper utilities, and menus do not exist in this secure workspace.

### 11.3 Hook Integrity
If a debugger or process tries to alter or remove the low-level keyboard hook handle, the WPF client shell intercepts the tamper attempt, terminates the compromised hook thread, spawns a new hook handler, and logs a critical security alert.

### 11.4 Process Integrity
The background service is registered with the Windows Service Control Manager to run under `NT AUTHORITY\SYSTEM` with Protected Process Light (PPL) attributes, preventing restricted interactive users from killing the process.

### 11.5 Overlay Integrity
The overlay renders inside the active game window or locked shell screen bounds. It does not accept keyboard or mouse inputs during active gameplay to prevent user interaction exploits.

### 11.6 Maintenance Authentication
Administrator bypass logins are processed through a hidden visual console window and validated using PBKDF2 password hashes with unique 32-byte salts, preventing dictionary attacks.

### 11.7 Policy Tamper Detection
Configuration files (`client_config.json`) are locked using DPAPI and validated against SHA-256 signatures generated with `server_public.key` on startup. If a signature mismatch is detected, the system locks down the workstation and alerts administrators.

---

## 12 Performance

### 12.1 Resource Allocations and Targets
*   **CPU Utilization Limit:** The background service and the locked visual shell must consume less than $0.5\%$ of total CPU cycles on a modern quad-core workstation.
*   **Memory Footprint Target:** The main service memory footprint must remain under 15MB, and the visual WPF shell must remain under 30MB of RAM.
*   **Hook Latency Target:** Low-level keyboard hook callbacks must process events within 1 millisecond. If a callback exceeds 5 milliseconds, the event is passed to prevent OS keyboard delay lags.
*   **Process Scan Frequency:** Deep local storage discovery scans are limited to system boots or run once daily on a low-priority thread to minimize disk IO overhead.
*   **Telemetry Sampling Interval:** Telemetry collectors sample hardware metrics (CPU, GPU, RAM) once every 10 seconds during active gameplay.

---

## 13 Windows Integration

This section defines the native Win32 APIs and kernel services used to implement the Phase 4 runtime management and kiosk controls.

### 13.1 Win32 API Map

| API Function | Library | Operational Domain | Usage Context |
| :--- | :--- | :--- | :--- |
| `CreateProcessAsUser` | `advapi32.dll` | Shell Lockdown | Spawns low-privilege interactive processes from Session 0. |
| `CreateJobObject` | `kernel32.dll` | Process Management | Instantiates a process grouping container. |
| `SetInformationJobObject`| `kernel32.dll` | Process Management | Applies resource caps and kill-on-close limits to Job Objects. |
| `AssignProcessToJobObject`| `kernel32.dll` | Process Management | Binds a process tree to a specified Job Object. |
| `SetWindowsHookEx` | `user32.dll` | Input Restrictions | Installs global keyboard hooks (`WH_KEYBOARD_LL`). |
| `CallNextHookEx` | `user32.dll` | Input Restrictions | Passes hook inputs to the next handler in the hook chain. |
| `UnhookWindowsHookEx` | `user32.dll` | Input Restrictions | Unregisters global keyboard hooks upon exiting lockdown. |
| `CreateDesktop` | `user32.dll` | Shell Lockdown | Spins an isolated Windows Desktop context. |
| `SwitchToDesktop` | `user32.dll` | Shell Lockdown | Switches active user display focus to the secure desktop. |
| `ClipCursor` | `user32.dll` | Mouse Restrictions | Pins mouse coordinate limits to a target window. |
| `ShowCursor` | `user32.dll` | Mouse Restrictions | Hides or shows the cursor pointer. |
| `WTSRegisterSessionNotification` | `wtsapi32.dll` | Windows Integration | Subscribes to terminal user session lock/unlock events. |
| `RegisterPowerSettingNotification` | `user32.dll` | Windows Integration | Receives notifications when hardware power states transition. |

### 13.2 DXGI and DirectX Overlay Integrations
The in-game overlay wraps Direct3D swap chains by hooking the `IDXGISwapChain::Present` function. It injects a custom widget into the graphics pipelines of running games. If DirectX hooks are blocked by game anti-cheat engines (e.g. Easy Anti-Cheat), the system falls back to spawning a transparent, topmost borderless WPF window configured with `WS_EX_TRANSPARENT` and `WS_EX_NOACTIVATE` styles to prevent stealing focus.

### 13.3 Device & Network Notifications
*   **`WM_DEVICECHANGE` & `SetupAPI`:** Intercepts USB insertion events. If a USB device matches storage class GUIDs (`GUID_DEVINTERFACE_DISK`), the system unmounts the volume to prevent unauthorized access.
*   **Raw Input API:** Captures keyboard and mouse events directly from hardware devices, bypassable only by administrators in Maintenance Mode.

---

## 14 Configuration

Below is the complete configuration schema template (`client_config.json`), locked at rest using DPAPI with machine-specific entropy.

```json
{
  "VersionCode": 40001,
  "WorkstationId": "STATION_WIN_101",
  "KioskPolicies": {
    "EnableKioskLockdown": true,
    "BlockKeyboardHooks": true,
    "ClipMouseBounds": true,
    "BlockTaskManager": true,
    "BlockUsbStorage": true,
    "BlockCommandPrompts": true,
    "AutoLockoutTimeoutMinutes": 20,
    "DefaultSecureDesktopName": "SAYRA_SECURE_DESKTOP",
    "RegistryLockdownKeys": [
      {
        "Hive": "HKCU",
        "KeyPath": "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System",
        "ValueName": "DisableTaskMgr",
        "ValueType": "DWORD",
        "DesiredValue": 1
      },
      {
        "Hive": "HKLM",
        "KeyPath": "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon",
        "ValueName": "Shell",
        "ValueType": "SZ",
        "DesiredValue": "Sayra.UI.exe"
      }
    ],
    "BlacklistedProcessNames": [
      "cmd.exe",
      "powershell.exe",
      "regedit.exe",
      "taskmgr.exe",
      "mmc.exe",
      "control.exe"
    ],
    "WhitelistedProcessNames": [
      "Sayra.UI.exe",
      "SayraClient.exe",
      "Sayra.Client.Guardian.exe",
      "Steam.exe",
      "EpicGamesLauncher.exe",
      "RiotClientServices.exe"
    ]
  },
  "LaunchPolicies": {
    "MaxLaunchTimeoutSeconds": 30,
    "EnableRegistryVirtualization": true,
    "FileMappingRedirections": [
      {
        "SourcePath": "%USERPROFILE%\\Documents\\My Games",
        "TargetPath": "%PROGRAMDATA%\\SAYRA_Client\\Saves"
      }
    ]
  },
  "ResourcePolicies": {
    "DedicatedShellCoresMask": 3,
    "DefaultGameCoresMask": 252,
    "EnforceMemoryLimitCaps": true,
    "MaxSystemRamAllocationBytes": 34359738368,
    "DefaultProcessPriorityClass": "HIGH"
  },
  "RecoveryPolicies": {
    "MaxCrashRetryLimit": 3,
    "StateReconciliationIntervalSeconds": 30,
    "WatchdogHeartbeatTimeoutSeconds": 5,
    "EmergencyLockdownOnTamper": true
  },
  "OverlayPolicies": {
    "EnableInGameOverlay": true,
    "PreferredOverlayLayout": "Compact",
    "OverlayOpacity": 0.85,
    "FpsCapLimit": 60,
    "EnableWpfFallbackOverlay": true
  }
}
```

---

## 15 Telemetry

All runtime operations, violations, and performance states are recorded to the local SQLCipher telemetry database and sent to the server.

### 15.1 Telemetry Event Schemas

#### 15.1.1 Game Lifecycle Events (`GameLifecycleEvent`)
```json
{
  "EventId": "e1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c",
  "SessionId": "SESS-WIN-99201",
  "GameId": "STEAM_CSGO_2",
  "ProcessId": 10244,
  "EventType": "Started",
  "LaunchDurationMs": 2450,
  "CommandLineArguments": "-novid -tickrate 128",
  "Timestamp": "2026-10-18T14:30:00.124Z"
}
```

#### 15.1.2 Kiosk Policy Violation Events (`KioskViolationEvent`)
```json
{
  "EventId": "f1e2d3c4-b5a6-4f7a-8b9c-0d1e2f3a4b5c",
  "SessionId": "SESS-WIN-99201",
  "ViolationType": "KeyboardHookBypassAttempt",
  "TriggeredKeyCode": 9,
  "TriggeredModifiers": "Alt",
  "ActionTaken": "BlockedAndLog",
  "Timestamp": "2026-10-18T14:35:12.450Z"
}
```

#### 15.1.3 Runtime Crash Events (`RuntimeCrashEvent`)
```json
{
  "EventId": "a1b2c3d4-e5f6-4f7a-8b9c-0d1e2f3a4b5c",
  "SessionId": "SESS-WIN-99201",
  "GameId": "EPIC_FORTNITE_1",
  "ExitCode": -1073740791,
  "ExecutionDurationSeconds": 142.5,
  "CrashDumpCreated": true,
  "DumpFilePath": "C:\\ProgramData\\SAYRA_Client\\Dumps\\fortnite_1024.dmp",
  "Timestamp": "2026-10-18T14:45:22.990Z"
}
```

#### 15.1.4 Resource Performance Snapshot Events (`ResourcePerformanceSnapshot`)
```json
{
  "EventId": "c1d2e3f4-a5b6-4f7a-8b9c-0d1e2f3a4b5c",
  "SessionId": "SESS-WIN-99201",
  "CpuUsagePercentage": 42.5,
  "RamUsageMb": 8192.0,
  "GpuUsagePercentage": 92.0,
  "GpuTemperatureCelsius": 72.0,
  "NetworkLatencyMs": 14,
  "Timestamp": "2026-10-18T14:50:00.000Z"
}
```

---

## 16 Testing Strategy

Every runtime component must be verified against failure conditions using automated unit, integration, and security tests.

### 16.1 Automated Testing Matrix

| Test Module | Target Method | Verification Criteria | Expected Outcome |
| :--- | :--- | :--- | :--- |
| **Unit Tests** | `Verify_KeyboardHook_Blocks_AltTab` | Installs low-level hook and injects virtual keystrokes. | Hook returns non-zero (`1`) to drop the event from the OS chain. |
| **Unit Tests** | `Verify_JobObject_Terminates_ChildTree` | Spawns dummy parent process that launches children inside Job. Closes Job handle. | Parent and all children are terminated instantly. |
| **Unit Tests** | `Verify_GameDiscovery_Parses_Steam_Vdf` | Feeds a mock `libraryfolders.vdf` and `acf` manifest. | Discovers game details and registers the profile. |
| **Integration** | `Verify_CreateDesktop_Creates_SAYRA_DESKTOP`| Invokes `SpawnSecureDesktopAsync`. Queries desktop lists. | Desktop named `SAYRA_SECURE_DESKTOP` exists. |
| **Integration** | `Verify_RawInput_Triggers_IdleReset` | Simulates raw physical mouse click and evaluates idle timer. | Active idle timeout counter resets to zero. |
| **Integration** | `Verify_ETW_Triggers_On_NewProcess` | Spawns a process. Monitors ETW kernel tracing subscription. | Process creation event is detected in less than 100ms. |
| **Chaos Tests**| `Verify_ServiceRecovery_On_Crash` | Forcefully kills the main service process. Monitors Guardian status. | Guardian restarts the main service process within 1,000ms. |
| **Security** | `Verify_Unauthorized_Cmd_Spawning_Blocked` | Attempts to spawn `cmd.exe` from a standard low-privilege user session. | ETW interceptor terminates `cmd.exe` instantly, blocking execution. |

---

## 17 Acceptance Criteria

Every subsystem must pass the following binary (pass/fail) criteria:

### 17.1 Shell Lockdown & Kiosk Controls
*   **ALT+TAB Blocked:** Pressing `Alt+Tab` while the kiosk is active must return a block result and must not show task-switching panels.
*   **WINKEY Ignored:** Pressing the Windows key must not show the Start Menu or trigger taskbar searches.
*   **Explorer Shell Replacement:** The `Shell` registry key must point to the custom shell executable upon booting.
*   **Task Manager Disabled:** Opening Task Manager via standard options must return an access-disabled notification.
*   **Secure Desktop Activation:** Switching to standard desks is blocked while active player sessions are locked.

### 17.2 Process Supervision & Job Objects
*   **Automatic Child Cleanup:** Closing a game process inside a Job Object must automatically clean up associated launcher helper processes.
*   **Dynamic Thread Affinity:** Spawning a game process tree must constrain thread execution to core masks defined in `client_config.json`.
*   **Memory Cap Enforcement:** Exceeding Job Object physical memory limits must trigger safe termination rather than system-wide out-of-memory crashes.

### 17.3 Overlays & UI
*   **Direct3D Integration:** Overlays must render timers and status details inside Direct3D games.
*   **WPF Fallback Activation:** If Direct3D hooks are blocked, the topmost transparent WPF window overlay must activate.

---

## 18 Risks

### 18.1 Kiosk Escape Vulnerabilities
*   *Risk:* Users with local administrative privileges may use advanced debugger shortcuts or recovery tools to bypass Kiosk Mode.
*   *Mitigation:* Workstation user sessions run as standard restricted users, not administrators, and key system registry paths are monitored to prevent alterations.

### 18.2 Antivirus Conflicts
*   *Risk:* Antivirus tools may flag low-level keyboard hooks (`WH_KEYBOARD_LL`) as keylogging malware.
*   *Mitigation:* Formal code-signing certificates are applied to client binaries, and installer packages register binaries with Windows Defender exclusions.

### 18.3 DXGI Overlay Conflicts
*   *Risk:* Anti-cheat engines (e.g. Easy Anti-Cheat, BattlEye) may flag D3D Present hooks as cheating overlays.
*   *Mitigation:* The overlay engine is configured to use the WPF transparent topmost window fallback overlay if Direct3D hooks are blocked.

### 18.4 Multi-Monitor Escapes
*   *Risk:* Users may move mouse pointers to secondary monitors during gameplay and click to steal window focus.
*   *Mitigation:* The `InputRestrictionService` restricts mouse pointer boundaries using the Win32 `ClipCursor` API, pinning movement coordinates to the active game viewport.

---

## 19 Future Integration

The Phase 4 Game Runtime Management & Kiosk Control subsystem forms a secure execution layer that integrates with future development phases:

```
+-----------------------------------------------------------------------------------------+
|                                FUTURE INTEGRATION ROADMAP                               |
+-----------------------------------------------------------------------------------------+
│                                                                                         │
│  - Phase 5: Admin Integration                                                           │
│    -> Connects IMaintenanceModeService and IKioskSecurityService to remote admin control│
│       commands received over TLS 1.3 socket connections.                                │
│                                                                                         │
│  - Phase 6: Update Platform                                                             │
│    -> Coordinates update downloads by temporarily suspending Kiosk policies during system│
│       maintenance intervals, automatically re-locking upon completion.                  │
│                                                                                         │
│  - Phase 7: Enterprise Operations                                                       │
│    -> Integrates with centralized log management, streaming runtime performance details │
│       and hardware diagnostics to central dashboards.                                  │
+-----------------------------------------------------------------------------------------+
```

---

## 20 Implementation Checklist

### Epic 1: Custom Shell & Kiosk Lockdowns (P0)
*   **Feature 1.1: Custom Shell Registry & Winlogon Integrations**
    *   *Task:* Write registry configurations to set the custom shell path during active workstation operations.
    *   *Subtask:* Implement fallback systems to restore `explorer.exe` upon entering Maintenance Mode.
*   **Feature 1.2: Secure Desktop Deployments**
    *   *Task:* Implement native Win32 `CreateDesktop` wrapper utilities to spin isolated desktops.
    *   *Subtask:* Establish desktop transition hooks using `SwitchToDesktop`.

### Epic 2: Global Keyboard Hook Restrictions (P0)
*   **Feature 2.1: WH_KEYBOARD_LL Interceptors**
    *   *Task:* Implement global low-level hooks in C# targeting `SetWindowsHookEx`.
    *   *Subtask:* Intercept and block designated escape keyboard combinations (Alt+Tab, WinKey, Win+R).

### Epic 3: Game Launch & Job Objects (P0)
*   **Feature 3.1: Windows Job Objects Integration**
    *   *Task:* Build native Win32 wrappers for `CreateJobObject` and `SetInformationJobObject`.
    *   *Subtask:* Configure Job Objects with `JO_LIMIT_KILL_ON_JOB_CLOSE` flags to clean up child processes.
*   **Feature 3.2: CreateProcessAsUser Pipeline**
    *   *Task:* Implement process spawning utilities from Session 0 utilizing restricted interactive tokens.

### Epic 4: In-Game Overlay Systems (P1)
*   **Feature 4.1: Direct3D DXGI SwapChain Hooking**
    *   *Task:* Develop native C++ present hook assemblies to inject widgets into DXGI pipelines.
*   **Feature 4.2: WPF Fallback Overlay**
    *   *Task:* Create topmost transparent WPF overlay windows configured with non-activating window styles.

---

## 21 Deliverables

The complete Phase 4 development bundle must deliver the following security, lockdown, and execution artifacts:

### 21.1 Core Domain Entities & Models
*   `GameProfile.cs` (Discovered game details)
*   `LaunchProfile.cs` (Spawning arguments and parameters)
*   `RuntimeSession.cs` (Active user session details)
*   `ProcessTree.cs` (Process parent-child hierarchy map)
*   `OverlayState.cs` (Overlay visual rendering properties)
*   `KioskPolicy.cs` (Kiosk lockdown rules and configurations)
*   `MaintenanceSession.cs` (Maintenance mode audit records)
*   `GameIntegrityState.cs` (Code validation tracking logs)
*   `CrashReport.cs` (Diagnostic telemetry logs)
*   `RuntimeResourceSnapshot.cs` (Hardware telemetry metrics)

### 21.2 Service Interfaces & Implementations
*   `IGameDiscoveryService.cs` / `GameDiscoveryService.cs` (Scans and indexes game libraries)
*   `IGameLaunchService.cs` / `GameLaunchService.cs` (Prepares launch environments and launches processes)
*   `IProcessSupervisor.cs` / `ProcessSupervisor.cs` (Tracks process states and implements Job Objects)
*   `IKioskSecurityService.cs` / `KioskSecurityService.cs` (Enforces shell locks and desktop isolation)
*   `IOverlayService.cs` / `OverlayService.cs` (Handles DXGI hooks and WPF overlay fallbacks)
*   `ISessionEnforcementService.cs` / `SessionEnforcementService.cs` (Tracks session durations and handles logouts)
*   `IGameIntegrityService.cs` / `GameIntegrityService.cs` (Monitors whitelists and blocks cheat processes)
*   `IRuntimeRecoveryService.cs` / `RuntimeRecoveryService.cs` (Handles system recovery and state syncs)
*   `IInputRestrictionService.cs` / `InputRestrictionService.cs` (Manages global keyboard/mouse hooks)
*   `IMaintenanceModeService.cs` / `MaintenanceModeService.cs` (Provides bypass controls for administrators)

### 21.3 Windows Integration Libraries & Hooks
*   `DxgiPresentHook.dll` (Direct3D swap chain hook assembly)
*   `LowLevelKeyboardHookProvider.cs` (Global WH_KEYBOARD_LL interceptor)
*   `LowLevelMouseConfiner.cs` (ClipCursor restrictor)

### 21.4 Configuration & Manifest Templates
*   `client_config.json` (Local DPAPI configuration file template)
*   `local_admin.json` (Local PBKDF2 administrative bypass credential store)

### 21.5 Complete Testing Suite Portfolio
*   `Sayra.Client.Tests.Runtime/` (Automated suite containing 45 unit, integration, and security bypass tests)

---
*End of Specification Document.*
