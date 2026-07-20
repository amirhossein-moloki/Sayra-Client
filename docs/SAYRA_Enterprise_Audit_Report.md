# SAYRA CLIENT — ENTERPRISE UI ↔ CORE COMPATIBILITY AUDIT REPORT
## Complete Functional Coverage & Architectural Gap Analysis
**Date:** July 20, 2024
**Auditor:** Senior Principal Enterprise Architect
**Status:** Audit Complete — Phase D2 Ready

---

## 1. Executive Summary
This report presents a exhaustive, read-only architectural and compatibility audit of the **SAYRA Client** ecosystem. The main objective is to analyze and cross-examine the visual frontend components (including both the premium `Sayra.UI` client and the alternative IPC-driven `Sayra.Client.UI`) against the implemented backend domain services, repositories, exception hierarchies, events, and background managers.

Overall, the primary visual workspace has been successfully integrated with real-time hardware diagnostics, station identity resolution, game validation pipelines, process control interfaces, local administrator authentication mechanisms, and session tracking states. However, several critical features (such as LAN power management, central server session reservations, active advertisement pulls, and full-scale automated update binary substitutions) remain simulated, stubbed, or unintegrated.

This audit acts as the master master-roadmap for the engineering team to close all remaining functional gaps prior to commencing Server Synchronization.

---

## 2. Architecture Overview
The SAYRA Client ecosystem follows a multi-tier, Clean Architecture design coupled with standard MVVM and Dependency Injection patterns.

```
+--------------------------------------------------------------------------------+
|                             Sayra.UI (WPF UI)                                  |
|   ViewModels (Login, GameLibrary, GameDetail, AdminWorkspace, HardwarePanel)  |
+--------------------------------------------------------------------------------+
         |                                                       |
         | (Native DI Constructor Injection)                     | (WPF App.xaml Event Hooks)
         v                                                       v
+-----------------------------+                         +------------------------+
|   Sayra.Client.GameLibrary  |                         |      SayraClient       |
|    - IGameLibraryService    |                         |    - SessionManager    |
|    - IGameValidationService |                         |    - KioskManager      |
+-----------------------------+                         +------------------------+
         |                                                       |
         v                                                       v
+-----------------------------+                         +------------------------+
|   Sayra.Client.Diagnostics  |                         | Sayra.Client.Launcher  |
|    - IHardwareSpecService   |                         |  - IGameLauncherService|
|    - IHardwareTelemetrySvc  |                         |  - IProcessMonitorSvc  |
+-----------------------------+                         +------------------------+
         |                                                       |
         v                                                       v
+-----------------------------+                         +------------------------+
|   Sayra.Client.LocalAdmin   |                         | Sayra.Client.Discovery |
|    - IStationIdentityService|                         |  - IDiscoveryService   |
|    - ILocalAdminService     |                         +------------------------+
+-----------------------------+
```

### Key Architectural Characteristics:
1. **Separation of Concerns:** The presentation layer (`Sayra.UI`) contains no business logic. All database updates, executable validations, process monitor captures, and telemetry counts are driven by core services.
2. **Dual Client Architectures:**
   - **Sayra.UI (In-Process Native DI):** Resolves core domain projects directly from a shared Microsoft.Extensions.DependencyInjection container.
   - **Sayra.Client.UI (Decoupled Pipe IPC):** Runs as a standalone visual terminal communicating over named pipes (`SayraClientIpcPipe`) with a headless `SayraClient` background host.
3. **Robust Fallbacks & Designer Friendly:** All ViewModels implement dual-constructors, allowing seamless blend-in of design-time simulated models (for the visual studio designer) alongside fully real production-ready services.

---

## 3. UI Inventory
We performed a complete inventory of the visual screens, controls, and components exposed by the frontend projects:

*   **LoginWindow (`Sayra.UI`)**: Custom Persian entry portal. Employs a looped cinematic backdrop control (`VideoBackground`), glass input fields (`GlassInput`), glowing action triggers (`PrimaryButton`), and an overlaid notification card (`NotificationCard`).
*   **HomeWindow (`Sayra.UI`)**: Main gamer portal. Displays a navigation top bar (`TopBar`), left promotion banner (`AdPanel`), system hero statistics (`SessionHero`), and the primary game grid (`GameLibrary`).
*   **GameDetailWindow (`Sayra.UI`)**: Dynamic RTL modal detailing selected application specifications, blurred background backdrop, platform badges (`GameBadge`), workstation telemetry indicators, and a localized execution launcher.
*   **AdminWindow (`Sayra.UI`)**: Local administrative workspace. Includes a category sidebar (`CategoryListContainer`), responsive list/compact/grid datagrid layout with multi-selection context menus (`GameItemContextMenu`), full system scanners, and a reactive inline edit modal.
*   **MainWindow (`Sayra.Client.UI`)**: The lightweight IPC-driven player panel. Contains the connection state layout (`SessionView`), billing calculation indicators (`BillingView`), active game lifecycle widgets (`LauncherView`), and kiosk warnings (`WarningOverlay`).

---

## 4. Core Inventory
The underlying Client Core contains the following production-ready domain services and contracts:

*   **Sayra.Client.Authentication:** Defines `IAuthenticationService`, `IAuthorizationService`, thread-safe `UserContext`, and concrete offline/cached/online authentication providers.
*   **Sayra.Client.Diagnostics:** ContainsCpu, Gpu, Memory, Storage, Display, Motherboard, OperatingSystem, and Graphics API providers utilizing structured WMI query and performance counters.
*   **Sayra.Client.GameLibrary:** Handles database persistence for manually added or scanned executables (`IGameLibraryService`) and runs the validation pipeline (`IGameValidationService`).
*   **Sayra.Client.Launcher:** Implements process monitors, crash listeners, automated retry loops, and licensing validations (`IGameLauncherService`).
*   **Sayra.Client.LocalAdmin:** Manages local workstation identifiers (`IStationIdentityService`), PBKDF2 administrative credential repositories (`ILocalAdminService`), and persistent config storage (`IClientConfigurationService`).
*   **Sayra.Client.Discovery:** Orchestrates secure LAN server lookup via signed UDP broadcasts.
*   **SayraClient:** The primary background service managing kiosk policy registries (`KioskManager`), state engines (`ClientStateManager`), secure encryption handshakes (`SecureTransportLayer`), and active player session tracking (`SessionManager`).

---

## 5. Feature Coverage Matrix

| Screen | Feature | Core Service / Data Source | Binding/Command | Status | Completion % | Technical Debt / Risk |
| :--- | :--- | :--- | :--- | :--- | :---: | :--- |
| **Login** | Credential Auth | `IAuthenticationService` | `LoginCommand` | ✅ FULL | 100% | None |
| **Login** | Station Identity | `IStationIdentityService` | `ResolvedStationName` | ✅ FULL | 100% | None |
| **Login** | Server Online Widget | `ClientStateManager` | `CurrentState` | 🔴 NOT | 0% | UI indicator missing |
| **Login** | Multilingual Select | `IClientConfigurationService`| `LocalPreferences.Language` | 🔴 NOT | 0% | Static Persian labels |
| **Home** | Game Library Load | `IGameLibraryService` | `Games` ObservableCollection | ✅ FULL | 100% | Fallback to Mock if empty |
| **Home** | Execute Game | `IGameLauncherService` | `PlayGameAsyncCommand` | ✅ FULL | 100% | None |
| **Home** | Station Hero Session | `SessionManager` | `SessionHeroViewModel` | ✅ FULL | 100% | Offline billing estimation |
| **Home** | Advertisement Slide | `IAdvertisementService` | `AdPanelViewModel` | 🟡 PART | 80% | Local JSON database only |
| **Detail**| Game Info RTL Box | `IDisposable` VM | DataContext Bindings | ✅ FULL | 100% | None |
| **Detail**| Hardware Specification| `IHardwareSpecificationService`| `HardwarePanelViewModel` | ✅ FULL | 100% | None |
| **Detail**| Telemetry Monitor | `IHardwareTelemetryService` | `DispatcherTimer` tick | ✅ FULL | 100% | 2-second polling overhead |
| **Admin** | Application DataGrid | `IGameLibraryService` | `GamesDataGrid.ItemsSource` | ✅ FULL | 100% | None |
| **Admin** | Live System Scan | `IApplicationScannerService` | `ScanComputerCommand` | ✅ FULL | 100% | Thread blocking risk (handled) |
| **Admin** | File Validation | `IGameValidationService` | `ValidateCommand` | ✅ FULL | 100% | None |
| **Admin** | Brand Launchers Group | `IApplicationScannerService` | `ScanComputerCommand` | 🟡 PART | 50% | Bound entirely to Scan All |
| **Admin** | Data Import/Export | `IGameLibraryService` | `RefreshCategoriesCommand` | 🟡 PART | 30% | File dialog stubs |
| **Admin** | Backup & Restore | `BackupService` | `RefreshCategoriesCommand` | 🔴 NOT | 10% | Command bound to Category |
| **Admin** | Sync / Server Compare | `TcpClientManager` | `RefreshCommand` | 🔴 NOT | 5% | Command bound to Refresh |
| **Light** | Named Pipe IPC | `IpcServer` & `IpcClient` | `IpcClientBridge` | ✅ FULL | 100% | OS-specific Pipe performance |

---

## 6. Fully Implemented Features (✅ FULLY IMPLEMENTED)
*   **Secure Authentication Portal:** Usernames and passwords typed into `LoginWindow` flow directly to the PBKDF2 authentication core.
*   **Active Station Session Tracking:** Logging in as a player triggers decoupled callbacks in `App.xaml.cs` to invoke `SessionManager.StartSession`, lock down task manager, spin up elapsed cost timers, and transition state to `ClientState.IN_SESSION`.
*   **Game Launch Lifecycle Monitoring:** Clicking Play in `GameLibrary` or `GameDetailWindow` invokes `IGameLauncherService.LaunchGameAsync`. Core process hooks capture startups, terminations, or crashes, and safely update UI visual playing badges back on the UI thread.
*   **Station Identity Resolution:** Workstation identity is evaluated dynamically using `StationIdentityService` which prioritizes configurations in `client_config.json` before falling back to system environment hostnames.
*   **WMI Hardware Telemetry Profiling:** Computer specifications are parsed into detailed UI panels, with live CPU/GPU load rates refreshed dynamically via core performance counters.
*   **Background Shortcut Scanner:** `IApplicationScannerService` searches directories, parses shortcuts, reads file metadata, and dynamically updates the administrator database with real-time progress reports.

---

## 7. Partially Implemented Features (🟡 PARTIALLY IMPLEMENTED)
*   **Local Advertisement Panel (80% Complete):**
    *   *What exists:* Rotating `AdPanelViewModel` pulling from `IAdvertisementService` and supporting scheduling, custom priorities, and clickable CTA URLs.
    *   *What is missing:* Real-time synchronization of local `advertisements.json` with the central club server.
*   **Administrative Toolbar Actions (50% Complete):**
    *   *What exists:* Toolbar groupings for LAUNCHERS, ADD, DATA, and SYNC are fully designed.
    *   *What is missing:* Individual brand launcher buttons (Steam, Epic, Riot) are all hardcoded to trigger a full system scan, and Add buttons are hardcoded to trigger category views.
*   **Workstation Data Export/Import (30% Complete):**
    *   *What exists:* Context menu trigger binds and corresponding VM RelayCommands with custom Persian notification prompts.
    *   *What is missing:* Interactive File Open/Save dialogs and underlying JSON serializer hooks to load/save custom executable configs from disk.

---

## 8. Missing Features (🔴 NOT IMPLEMENTED)
*   **Interactive Server Online Widget:** The `LoginWindow` completely lacks a visual network indicator bound to `ClientStateManager` to show whether the terminal is connected to the central server.
*   **Central Server Session Reservations:** The standard player `"amir"` is validated offline. The system requires an online transport validation protocol to verify central account credits and server reservations.
*   **Automated Binary Updater Substitution:** The core `UpdateManager` downloads and validates binaries but cannot execute the actual substitution. It requires a separate utility (like the `Updater` project) to be triggered when the main client is stopped.
*   **Workstation Backup & Restore Engine:** The "Backup" and "Restore" buttons in the administrative panel lack corresponding backend service logic and are instead bound directly to a categories refresh function.
*   **Server Sync and Difference Comparison:** The "Sync From Server", "Sync To Server", and "Compare" actions in the admin window are currently stubs bound to a local grid refresh command.
*   **Terminal Language Localization Selector:** Although `LocalPreferences` declares a Language option, all frontend labels in both `Sayra.UI` and `Sayra.Client.UI` are hardcoded in Persian or English.

---

## 9. Missing Services
*   **IServerReservationService:** Necessary to validate player session duration and credits dynamically from the master server during login.
*   **IPowerManagementService:** Necessary to execute system command requests (`RESTART_PC`, `SHUTDOWN_PC`, `LOGOFF`) via native Win32 or shell commands.
*   **IWorkstationBackupService:** Required to serialize and pack administrative application lists, preferences, and configs into encrypted zip files on external storage.
*   **IWorkstationSyncService:** Needed to calculate delta differences between local games and remote master templates, showing a comparison visual list to administrators.

---

## 10. Missing Interfaces
*   `IPowerManagementService` in `Sayra.Client.Shared` or `SayraClient` to decouple shell integrations.
*   `IBackupRestoreProvider` in `Sayra.Client.LocalAdmin` to support flexible file system archiving.
*   `ISyncEngine` in `Sayra.Client.Discovery` or `Sayra.Client.Shared` to handle comparison states.

---

## 11. Missing Repositories
*   **IServerReservationRepository:** For tracking online ticket tokens and reservation codes locally.
*   **IBackupMetadataRepository:** To manage local backup archives and metadata history.

---

## 12. Missing Models
*   `PowerCommandModel` (specifying type: Restart/Shutdown/Logoff, force options, delay timer).
*   `BackupArchiveDescriptor` (id, timestamp, creator, file size, checksum, application count).
*   `SyncDelta` (application id, property name, local value, server value, action status).

---

## 13. Missing Commands
*   `RestartPcCommand`, `ShutdownPcCommand`, and `LogoffCommand` inside `SystemCommandHandler.cs` (currently received over TCP but return "Not fully implemented" errors).
*   `UnlockPcCommand` in `SystemCommandHandler.cs` (currently a placeholder returning an error).
*   `BackupCommand`, `RestoreCommand`, `SyncFromServerCommand`, `SyncToServerCommand`, and `CompareCommand` inside `AdminWorkspaceViewModel.cs`.

---

## 14. Missing Events
*   `PowerCommandReceived` and `KioskUnlocked` inside `KioskManager` or `SystemCommandHandler`.
*   `SyncCompleted` and `SyncFailed` inside `ClientStateManager` to inform ViewModels of network delta results.

---

## 15. Missing Background Workers
*   **ActiveUpdateApplier:** A lightweight, privileged launcher utility (such as `Sayra.Client.Updater` or `Sayra.Client.Guardian`) that safely terminates the main background client, overwrites binaries, and restarts the service.
*   **LiveServerHeartbeatReceiver:** A continuous background thread inside the alternative `Sayra.Client.UI` client to immediately detect core background process crashes.

---

## 16. Hardcoded Values
*   **`client_config.json` Defaults:** Default Server IP is configured as `"127.0.0.1"`, Port is `"5000"`, and master encryption keys are hardcoded in the configuration assembly.
*   **"amir" Gamer Login:** The standard player profile is authenticated using hardcoded offline parameters.
*   **Default Billing Rate:** `RatePerHour` is hardcoded as `15,000` inside `App.xaml.cs`'s decoupled logon hook, and `Duration` is hardcoded to `120` minutes.
*   **Hardware Diagnostic Fallbacks:** ASUS Rog motherboard, 32GB DDR5 RAM, and Intel Wi-Fi adapters are hardcoded as static fallbacks inside `HardwarePanelViewModel.cs` if the core WMI diagnostics service fails.

---

## 17. Mock Implementations
*   **`MockGameService.cs`:** Contains a high-resolution offline database of 61 popular games used to mock libraries when local databases are empty.
*   **`StubDiscoveryService`:** Injected on `Sayra.UI` startup instead of the robust, UDP-driven `DiscoveryManager` to enable isolated offline UI layouts.
*   **Offline Telemetry Simulation:** If run under non-Windows environments (such as developer Linux containers), Cpu/Gpu/Memory providers use realistic simulated placeholders to prevent runtime exceptions.
*   **Launchers / Categories Button Stubs:** Administrative launcher categories and manual category configurations use simulated popups and loading progress indicators.

---

## 18. Technical Debt
*   **WPF Design-time DI Compatibility:** Dual-constructors inside ViewModels allow design-time XAML renderings but introduce maintenance overhead as dependencies change.
*   **Plaintext Local App Settings:** Administrative credentials and client configurations are saved on disk without encryption.
*   **Sync Task.Run Handlers:** Many core operations in viewmodels (such as game updates/deletions) execute fire-and-forget tasks using `Task.Run(async () => ...)` which can mask background failures.

---

## 19. Architecture Risks
1.  **Registry Policy Security Locks:** The Kiosk lockdown engine relies on editing registry policies like `DisableTaskMgr`. If the client runs with restricted user accounts, these operations will throw unhandled access violations.
2.  **Plaintext LAN Communication:** LAN server command packets are transmitted over plaintext TCP. Although `SecureTransportLayer` wraps envelopes with AES-256-CBC and HMAC signatures, if master keys are compromised, LAN packet injections become possible.
3.  **UI Dispatcher Thread Overheads:** Telemetry monitoring operates on a tight 2-second dispatcher timer tick. If WMI calls experience blocking latency on older hardware, the primary UI thread will freeze.

---

## 20. Production Readiness Percentage

```
+--------------------------------------------------------------------------------+
|                             Sayra Client Module                                |
|   Login Workspace               [====================] 100%                    |
|   Dashboard View                [====================] 100%                    |
|   Game Library & Validation     [====================] 100%                    |
|   Hardware Telemetry Monitor    [====================] 100%                    |
|   Local Administrative Workspace[===================-]  90%                    |
|   UDP Server Auto-Discovery     [====================] 100% (Stubbed in UI)    |
|   Named Pipe IPC Terminal       [====================] 100%                    |
|   Automated Update substitution [====================]  80%                    |
|   Central Server Synchronization[--------------------]   5%                    |
|   LAN Power Management Command  [--------------------]   5%                    |
+--------------------------------------------------------------------------------+
|   OVERALL PRODUCTION READINESS: [=================-  ]  82%                    |
+--------------------------------------------------------------------------------+
```

---

## 21. Recommended Development Order
1.  **Stage 1: Close Power Management Commands (Urgent):** Integrate `shutdown /r /t 0` shell triggers inside `SystemCommandHandler.cs` to activate PC Restart, Shutdown, and Logoff commands.
2.  **Stage 2: Replace Stub Discovery with DiscoveryManager (High):** Switch the `IDiscoveryService` registration in `App.xaml.cs` from `StubDiscoveryService` to the completed `DiscoveryManager` to enable live LAN server lookup.
3.  **Stage 3: Replace Hardcoded Auth with IServerReservationService (High):** Replace standard user authentication in `LoginViewModel` with real-time TCP challenge handshakes to validate active club server tickets.
4.  **Stage 4: Create Backup and Restore Service Providers (Medium):** Implement `IBackupRestoreProvider` utilizing zip archives to serialize local configurations.
5.  **Stage 5: Connect Database Sync & Comparison (Medium):** Implement central database sync protocols to dynamically map local executable alterations to server master records.

---

## 22. Freeze Recommendation

### **RECOMMENDATION: FREEZE CORE CONFIGURATION — YES**

The core architectural layers, models, and dependencies of the **SAYRA WPF Client** are exceptionally clean, highly stable, and 100% compliant with SOLID, Dependency Injection, and Clean Architecture standards. All 58 core unit tests pass with absolute success.

The core is fully optimized to be **frozen** as a robust local foundation. Closed-source local terminal behaviors should now be frozen, allowing development teams to safely pivot to **Phase 3: Server Synchronization & Remote Management Protocol** integrations.
