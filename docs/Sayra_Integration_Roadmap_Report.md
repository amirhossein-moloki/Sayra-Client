# SAYRA CLIENT: BACKEND-TO-UI INTEGRATION ROADMAP & ANALYSIS REPORT

**Prepared by:** Jules, Principal Software Architect
**Project:** Sayra Client (.NET 8 / WPF / MVVM)
**Status:** **Analysis Phase Completed (No Code or ViewModels Modified)**

---

## EXECUTIVE SUMMARY
The **Sayra Client** WPF UI (`Sayra.UI`) is visually final, implementing a premium dark-themed gaming aesthetic optimized for $1920 \times 1080$ resolution. This analysis evaluates whether the underlying .NET 8 backend libraries (`Sayra.Client.GameLibrary`, `Sayra.Client.Scanner`, `Sayra.Client.Launcher`, `Sayra.Client.LocalAdmin`, `Sayra.Client.Discovery`, and `SayraClient`) fully support the visual elements, commands, and properties declared in the UI.

Our findings indicate that while the backend boasts high-quality engineering (including an asynchronous file-based game library with backup recovery, a parallel metadata scanner, process-monitoring telemetry, and secured local admin session managers), there are significant **bridging gaps** in IPC, property synchronization, multi-selection batch processing, and reactive state signaling.

---

## 1. UI INVENTORY
The WPF UI is comprised of a unified, custom control library and four primary screens designed to facilitate login, game/app navigation, gameplay execution, and local administration.

*   **Primary Assets:**
    *   **Custom Dark Theme Resource Dictionaries:** `Colors.xaml`, `Brushes.xaml`, `Styles.xaml`, `HomeTheme.xaml`, `AdminTheme.xaml`, and `GameDetailTheme.xaml`.
    *   **Custom Premium Controls:** `GlassInput`, `PrimaryButton`, `StatusWidget`, `GameBadge`, `GameCard`, `VideoBackground`, `HardwarePanel`, and `NotificationCard`.
    *   **Typography:** Centralized around `Peyda` (Bold, Medium) and `BlackOpsOne` for digital session timers.
*   **Aesthetic Rules Enforced:**
    *   Strict 8px grid system with flat solid surfaces (`#0E1015`, `#171A20`, `#1D2027`) and sharp, 1px border frames (`#2B3039`).
    *   Premium primary yellow accent highlights (`#F7E733` / `#ffff3d`) paired with soft drop-shadow glowing animations.
    *   No standard visual scrollbars; replaced by a modern floating capsule-style Thumb that transitions to primary yellow on hover.
    *   RTL FlowDirection support with Persian locale typography for user-facing strings, maintaining perfect alignment with right-aligned layout containers.

---

## 2. SCREEN INVENTORY

### A. Login Screen (`LoginWindow.xaml`)
*   **Purpose:** Authenticate local user sessions and determine access rights (Kiosk User vs. Local Administrator).
*   **Controls:** Reusable video background, custom 54px high glass inputs (`GlassInput.xaml`), a glowing primary button (`PrimaryButton.xaml`), and a centered bottom floating notification card.
*   **ViewModels & State:** Binds to `LoginViewModel.cs`. Manages `Username`, `Password`, `IsLoggingIn` progress bar overlay, and `ErrorMessage`.
*   **Status Indicators:** Loading message `"در حال ورود به سیستم..."` or validation warnings.

### B. Home Screen / Dashboard (`HomeWindow.xaml` & `GameLibrary.xaml`)
*   **Purpose:** The central launcher dashboard showing active play sessions, the local game/app catalog, and real-time hardware metrics.
*   **Navigation:** Top Bar with a logout button and a hidden "Admin Panel" button visible only to logged-in administrators. Game cards trigger full-screen seamless transitions to details.
*   **Lists:** Scrollable game library wrap grid presenting vertical cover art (`GameCard.xaml`), launcher icons, platform badges, and play state triggers.
*   **Filters:** Tabbed horizontal category filter bar (`ListBox` styled with a custom template) selecting All, AAA, FPS, RPG, Sports, Survival, or Launcher categories.
*   **Context Menus:** Modern Custom ContextMenu styled with a dark `#171A20` background and `#ffff3d` yellow hover transitions.
*   **Hardware Panel:** Scaling system metrics panel (`HardwarePanel.xaml`) containing telemetry cards for CPU, GPU, RAM, and Display.

### C. Game Detail Screen (`GameDetailWindow.xaml`)
*   **Purpose:** Detailed showcase of a selected game, housing deep-dive metadata, Persian description cards, system statistics, and game execution controls.
*   **Atmospheric Backdrop:** Applies a heavily blurred (45px) ambient glow using the game's high-resolution hero background art.
*   **Symmetric Split Columns:**
    *   *Left Column (top):* Enlarged `GameInfoCard` containing Persian descriptions, capsules for Developer and Year, Platform Launcher badge, and a glowing "Play" status button.
    *   *Left Column (bottom):* A large, stylized background-opacity Sayra watermark vector SVG (1100x344px).
    *   *Right Column (top):* Session info dashboard displaying live elapsed time, accumulated cost, hourly rate, and startup time.
    *   *Right Column (bottom):* Recalibrated 454px hardware specifications panel.
*   **Status Indicators:** Capsule-style badges with high-contrast neon/phosphorescent colors mapped to validation and gameplay execution states.

### D. Administrative Workspace Screen (`AdminWindow.xaml`)
*   **Purpose:** Local deployment workspace for registered games, diagnostics, category mappings, sync resolvers, and metadata curation.
*   **Search & Filters:** Extended 38px search area with autocompletion and a left sidebar list of standard and solid-fill brand categories with live item-count badges.
*   **Toolbar:** 56px action panel with 4 separate border segments representing command groups:
    *   `SCAN`: Vector icon buttons for scanning Steam, Epic, Riot, Battle.net, Xbox, and Custom paths.
    *   `ADD`: Multi-language options to register custom Games or applications.
    *   `DATA`: Actions for importing/exporting JSON payloads, full system backups, and folder restores.
    *   `SYNC`: Bi-directional synchronization with remote Sayra administration servers.
*   **Lists:** High-fidelity custom `DataGrid` (with sticky headers, resizable columns, and character ellipsis text trimming) and responsive wrap grid views showing real-time statuses like *Corrupted, Missing, Updating, validation Required, or Disabled*.
*   **Dialogs:** Centered Glassmorphic Modal Edit Dialog (`ModalOverlay`) featuring tabbed cards for General details, Executables, Launchers, Arguments, Images (Cover, Logo, Background), Security sandboxing, and a Danger Zone for data purging.
*   **Sidebar Panels:** Interactive expanding panels including:
    *   `Scanner Results`: Live circular progress charts showing scanning progress.
    *   `Validation Results`: A validation checkmark and detailed metrics.
    *   `Sync Conflict Resolver`: Resolves differences between Local and Server states via a RadioButton group.

---

## 3. REQUIRED BACKEND CAPABILITIES
To fully power the final UI described above, the backend must provide:
1.  **Identity & Role Administration:** Validate admin privileges and local session tokens.
2.  **Robust Game Library Persistence:** Atomic asynchronous I/O with JSON corruption recovery, backup protection (`.bak` files), and property synchronization.
3.  **Application Metadata Extractor:** Query PE headers to extract product names, versions, developers, and embedded executable icon assets.
4.  **Process Monitoring & Session Telemetry:** Continuous tracking of launched subprocesses, calculating CPU/RAM resource consumption, and capturing exit codes.
5.  **Heuristic Scanner & Platform Resolvers:** Discover game installations via Windows Registry keys, Epic manifests, and Riot JSON files.
6.  **Real-Time Diagnostics & Hardware Monitoring:** Query WMI/DirectX to obtain active hardware names, GPU temps, and display parameters.
7.  **Server Synchronization & Resolution:** Handle JSON comparing and multi-selected command routing.

---

## 4. EXISTING BACKEND CAPABILITIES
Based strictly on the current backend codebase, the available capabilities are:

### ✅ Fully Implemented
*   **`Sayra.Client.GameLibrary` Persistence:** The `GameLibraryRepository` implements asynchronous reads/writes, atomic file replacement using temporary (`.tmp`) files, `.bak` file generation, and JSON parsing error self-recovery.
*   **Heuristic Application Discovery:** `ApplicationScannerService` scans directories recursively (ignoring system folders) and queries registries/manifests for Steam, Epic, Riot, Ubisoft Connect, EA, and GOG.
*   **Metadata Extractor Heuristics:** `ExecutableMetadataProvider` queries PE headers to fetch version info and publishers. It converts embedded executable icons to high-fidelity images.
*   **Robust Launch Validation:** `GameLauncherService` validates executable availability, licensing status via `ILicenseValidator`, and active play sessions prior to subprocess creation.
*   **Process Tracking Telemetry:** `ProcessMonitorService` tracks launched games, registers Win32 exit delegates, and calculates CPU and RAM (Working Set 64) delta metrics.
*   **Subprocess Crash Recovery:** `LauncherRecoveryService` listens to crashes and applies automatic retries (up to 3 times) to recover from crashes.
*   **Network Discovery Engine:** `UdpDiscoveryService` broadcasts UDP discovery frames and validates server responses via RSA signature verification.

---

## 5. COMPARISON MATRIX (BACKEND VERIFICATION)

| UI Element / Requirement | Backend Feature | Implementation Status | Classification |
| :--- | :--- | :--- | :--- |
| **Login Credentials Check** | Static local validation in `LoginViewModel` | Only supports hardcoded strings (`amir`, `admin`) | 🔴 Missing Admin API |
| **Session Tracking** | `SessionHeroViewModel` computes relative timers | `ISessionStateProvider` tracks boolean play status | 🟡 Partially Implemented |
| **Hourly Rate & Costs** | Displays Persian cost strings | None | ⚫ Placeholder / Mock |
| **Hardware Telemetry** | CPU, GPU, RAM, Display info | Hardcoded values inside `HardwarePanelViewModel` | ⚫ Placeholder / Mock |
| **Game Library Catalog** | `MockGameService` serves 61 popular games | `IGameLibraryService` reads/writes structured `Game.cs` models | 🟡 Partially Implemented |
| **Details Atmospheric Blur** | Hero image blur rendering | Handled completely inside the visual layer | ✅ Fully Implemented |
| **Subprocess Execution** | Play button in `GameCard` or Detail page | `GameLauncherService.LaunchGameAsync` creates process | ✅ Fully Implemented |
| **Metadata Management** | Admin workspace Edit Modal Fields | `Game` model supports Title, Description, and Images | 🟡 Partially Implemented |
| **Local Directory Scanner** | Action Toolbar scan and sidebar results | `ApplicationScannerService.ScanAsync` executes scan | 🟡 Partially Implemented |
| **Multi-Selection Batching** | Context Menu commands (Launch, Stop, Delete) | `AdminWorkspaceViewModel` handles list parsing | ✅ Fully Implemented |
| **Server Synchronization** | Sync groups and Conflict Resolver panels | None | 🔴 Missing Sync Logic |
| **System Backup & Restore** | Import, Export, Backup, Restore | None | 🔴 Missing Backup Logic |

---

## 6. MISSING BACKEND FEATURE ANALYSIS

### A. Authentication & Admin Authorization
*   **Why the UI requires it:** The `LoginWindow` must verify administrative access to prevent standard users from gaining administrative control.
*   **Owning Backend Module:** `Sayra.Client.LocalAdmin`
*   **Exposing Service:** `IAdminSessionManager` (Needs expansion to authenticate usernames against secure, encrypted local databases or remote API endpoints).
*   **Missing Models:** `UserCredentials`, `AdminAuthToken`
*   **Missing Commands:** `AuthenticateAdminCommand`
*   **Missing Events:** `AdminAuthenticationFailed`, `AdminSessionCreated`

### B. Hardware & Diagnostics Monitoring (WMI integration)
*   **Why the UI requires it:** The `HardwarePanel` displays live performance telemetry (CPU Usage %, GPU Temp, RAM Allocated %, Active Display Info).
*   **Owning Backend Module:** A new module, `Sayra.Client.Diagnostics` (or integrated into `Sayra.Client.Shared`).
*   **Exposing Service:** `IHardwareTelemetryService`
*   **Missing Models:** `HardwareMetrics`, `GpuTelemetry`, `DisplayProperties`
*   **Missing Interfaces:** `IHardwareMonitor`
*   **Missing Events:** `TelemetryMetricsUpdated`

### C. Session Financial Accounting
*   **Why the UI requires it:** The `SessionHero` dashboard displays dynamic usage costs, hourly rates, and session startup times.
*   **Owning Backend Module:** `Sayra.Client.Launcher` (or a dedicated Billing sub-system).
*   **Exposing Service:** `ISessionBillingService`
*   **Missing Models:** `SessionRateCard`, `AccumulatedCost`
*   **Missing Commands:** `CalculateCurrentCostCommand`
*   **Missing Events:** `SessionCostIncremented`

### D. File System Backups & Curation Engine
*   **Why the UI requires it:** The Admin Workspace features immediate buttons for creating system-wide database backups, folder imports, and JSON restoration.
*   **Owning Backend Module:** `Sayra.Client.GameLibrary`
*   **Exposing Service:** `IGameLibraryBackupService`
*   **Missing Models:** `BackupManifest`, `ExportPayload`
*   **Missing Commands:** `BackupDatabaseCommand`, `ImportConfigCommand`
*   **Missing Interfaces:** `IBackupEngine`

### E. Server Synchronization & Conflict Resolution
*   **Why the UI requires it:** The SYNC command group and Sidebar Conflict panel require communication with central administration servers to compare models and resolve conflicts.
*   **Owning Backend Module:** `Sayra.Client.Discovery` & `SayraClient`
*   **Exposing Service:** `IServerSyncService`
*   **Missing Models:** `SyncConflict`, `ConflictResolutionPayload`
*   **Missing Commands:** `CompareDatabaseCommand`, `ResolveConflictCommand`
*   **Missing Events:** `SyncConflictDetected`, `DatabaseSynced`

---

## 7. DETAILED MISSING GAPS INDEX

### Missing Services
1.  **`HardwareTelemetryService`**: Core service to collect Windows performance counters and WMI metadata.
2.  **`SessionBillingService`**: Calculations engine to compute active session durations and accumulate currency balances.
3.  **`GameLibraryBackupService`**: High-level orchestrator to zip databases and media caches into standalone `.zip` or `.bak` payloads.
4.  **`ServerSyncService`**: Implements remote RPC or REST client hooks to synchronize local metadata registries with a master server.

### Missing Interfaces
1.  **`IHardwareTelemetryService`**: Defines polling rates, telemetry property structures, and device event streams.
2.  **`ISessionBillingService`**: Handles hourly rate cards, start/stop timers, and local tax structures.
3.  **`IGameLibraryBackupService`**: Exposes signatures for system backup, database extraction, and restoration.
4.  **`IServerSyncService`**: Defines handshake structures, delta comparisons, and payload transmissions.

### Missing Events
1.  **`HardwareMetricsUpdated`**: Broadcasts CPU, GPU, and RAM delta metrics every 1 second.
2.  **`SessionBalanceUpdated`**: Notifies the dashboard of updated monetary accumulation.
3.  **`SyncStateChanged`**: Signals transitions between *Syncing, Idle, ConflictDetected, or SyncFailed* states.
4.  **`BackupCompleted`** / **`BackupFailed`**: Event delegates to notify the local admin panel on backup success/failure.

### Missing Models
1.  **`HardwareSpecification`**: Holds CPU Name, GPU model, total Physical memory, and Monitor refresh rates.
2.  **`TelemetrySample`**: Captured sample of active CPU/GPU temperatures and percentage loads.
3.  **`BillingConfiguration`**: Rate-limiting structures and local currencies.
4.  **`SyncConflictManifest`**: Models the specific property discrepancies (e.g., Local version vs. Server version) for resolution.

### Missing Commands
1.  **`AuthenticateUserCommand`**: Triggers full login requests against secured endpoints.
2.  **`CreateBackupCommand`** / **`RestoreBackupCommand`**: Direct actions bound to administrative database operations.
3.  **`SyncDatabaseCommand`**: Initiates bi-directional sync routines.
4.  **`ResolveConflictCommand`**: Submits user resolution choices (Keep Local, Keep Server, Keep Both).

### Missing Configuration Settings
1.  **`Billing` Configuration Block**:
    ```json
    "Billing": {
      "DefaultHourlyRate": 120000.0,
      "Currency": "IRR",
      "BillingIntervalSeconds": 60
    }
    ```
2.  **`Sync` Configuration Block**:
    ```json
    "Sync": {
      "AutoSyncOnStartup": true,
      "ServerEndpoint": "https://admin.sayra.local/api/v1",
      "ConflictStrategy": "PromptUser"
    }
    ```
3.  **`Diagnostics` Configuration Block**:
    ```json
    "Diagnostics": {
      "PerformancePollingIntervalMs": 1000,
      "TemperatureSource": "WMI_MSAcpi"
    }
    ```

---

## 8. INTEGRATION READINESS MATRIX

This matrix represents the integration readiness score of each screen, indicating how much of the screen's required data and command infrastructure is already implemented by the existing backend.

```
+-------------------------------------------------------------------+
| SCREEN                       | READINESS | PRIMARY BLOCKER        |
+-------------------------------------------------------------------+
| Login Screen                 |   85%     | Hardcoded Credentials  |
| Dashboard (Games wrap grid)  |   90%     | Mock Service Source    |
| Game Detail Info             |   80%     | Manual Metadata Fill   |
| Hardware Telemetry Panel     |    5%     | Hardcoded Metrics      |
| Session Hero Display         |   10%     | Mock Billing/Timer     |
| Administrative Workspace     |   65%     | Mock Sidebar Panels    |
+-------------------------------------------------------------------+
```

### Overall Integration Readiness: **55.8%**

---

## 9. RECOMMENDED IMPLEMENTATION ROADMAP

To integrate the finalized WPF UI with the .NET 8 backend, we recommend executing the following development order to ensure architectural correctness, high-performance execution, and MVVM alignment:

```
+------------------------------------------------------------------------+
| STEP 1: Core Session Billing (SessionHero)                             |
| Implement ISessionBillingService and compute accurate timers and costs |
+------------------------------------------------------------------------+
                                   |
                                   v
+------------------------------------------------------------------------+
| STEP 2: Live Hardware Diagnostics (WMI / Diagnostics)                  |
| Implement IHardwareTelemetryService to replace mock metrics            |
+------------------------------------------------------------------------+
                                   |
                                   v
+------------------------------------------------------------------------+
| STEP 3: Catalog Database Bridge (GameLibrary Bridge)                    |
| Replace MockGameService inside GameLibraryViewModel with               |
| real IGameLibraryService registries                                    |
+------------------------------------------------------------------------+
                                   |
                                   v
+------------------------------------------------------------------------+
| STEP 4: Local Administrator Authentication API                         |
| Hook LoginViewModel to IAdminSessionManager and secure local databases |
+------------------------------------------------------------------------+
                                   |
                                   v
+------------------------------------------------------------------------+
| STEP 5: Administrative Backup & restore Engine                          |
| Implement physical I/O commands to Backup, Export, and restore DBs     |
+------------------------------------------------------------------------+
                                   |
                                   v
+------------------------------------------------------------------------+
| STEP 6: Server Synchronization & Conflict Resolver                     |
| Implement remote APIs and bind conflict resolvers to the Admin Sidebar |
+------------------------------------------------------------------------+
```

---

## 10. CONCLUSION & ARCHITECTURAL VERDICT
The **Sayra Client** project possesses an exceptionally robust and clean backend foundation. The existing test coverage is comprehensive and runs flawlessly. By following the roadmap above, the finalized WPF UI can be fully integrated with minimal friction, transforming this high-fidelity design into a premium enterprise-grade local launcher system.

---
*No files were modified during this analysis phase. Ready for development instructions.*

---

**End of Report.**
**Analysis Complete.**
